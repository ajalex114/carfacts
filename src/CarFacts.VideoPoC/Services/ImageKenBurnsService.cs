using CarFacts.VideoPoC.Models;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

namespace CarFacts.VideoPoC.Services;

/// <summary>
/// Replaces Pexels video clips with a Bing + Wikimedia Commons image slideshow.
/// For each segment: searches images, downloads the best ones, renders a Ken Burns
/// zoompan clip, and returns the segment with ClipPath pointing to the local mp4.
/// Images are never repeated across segments.
/// </summary>
public class ImageKenBurnsService(string ffmpegPath)
{
    private static readonly HttpClient Http;

    private const double ClipImageDuration    = 1.5;  // target seconds per image
    private const double MinImageDisplay      = 1.5;  // minimum seconds any image must be visible
    private const long   MinFileSizeBytes     = 50_000;  // 50 KB quality gate

    static ImageKenBurnsService()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect        = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression   = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        Http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        Http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        Http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    }

    /// <summary>
    /// For each segment: collect images, build a Ken Burns mp4 clip, return segment with ClipPath set.
    /// Images are never repeated across segments. A large pool is fetched upfront.
    /// </summary>
    public async Task<List<VideoSegment>> ResolveClipsAsync(
        List<VideoSegment> segments,
        string outputDir)
    {
        var resolved = new List<VideoSegment>();

        // Pre-fetch a large unique image pool upfront for all segments combined.
        // This avoids running dry when multiple segments share the same query.
        int totalNeeded = segments.Sum(s =>
            Math.Clamp((int)Math.Ceiling(s.Duration / ClipImageDuration), 1, 4));

        var distinctQueries = segments.Select(s => s.SearchQuery)
                                      .Distinct(StringComparer.OrdinalIgnoreCase)
                                      .ToList();

        Console.WriteLine($"⬇️   Pre-fetching image pool (need ~{totalNeeded} unique images)...");
        var poolDir  = Path.Combine(Path.GetTempPath(), $"kbpool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(poolDir);
        var imagePool = new Dictionary<string, Queue<string>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var usedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var query in distinctQueries)
            {
                int forThisQuery = segments
                    .Where(s => s.SearchQuery.Equals(query, StringComparison.OrdinalIgnoreCase))
                    .Sum(s => Math.Min(4, Math.Max(1, (int)(s.Duration / MinImageDisplay))));

                var paths = await FetchImagePoolAsync(query, forThisQuery, poolDir, usedUrls);
                imagePool[query] = new Queue<string>(paths);
                Console.WriteLine($"  → \"{query}\": {paths.Count} images fetched (need {forThisQuery})");
            }

            // Render each segment using images from the pool
            for (int i = 0; i < segments.Count; i++)
            {
                var seg     = segments[i];
                var tempDir = Path.Combine(Path.GetTempPath(), $"kbclip-{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);
                Console.Write($"  🎬  [{i+1}/{segments.Count}] ({seg.Duration:F1}s)... ");

                try
                {
                    // How many images fit while keeping each ≥ MinImageDisplay seconds?
                    int needed = Math.Min(4, Math.Max(1, (int)(seg.Duration / MinImageDisplay)));
                    var pool   = imagePool.GetValueOrDefault(seg.SearchQuery) ?? new Queue<string>();
                    var images = new List<string>();
                    while (images.Count < needed && pool.Count > 0)
                        images.Add(pool.Dequeue());

                    if (images.Count == 0)
                        throw new InvalidOperationException("Image pool exhausted");

                    // Distribute segment duration evenly — every image gets equal screen time
                    double perImage = seg.Duration / images.Count;

                    var clipPaths = new List<string>();
                    for (int j = 0; j < images.Count; j++)
                    {
                        var imgClip = Path.Combine(tempDir, $"img_{j:D2}.mp4");
                        await MakeKenBurnsClipAsync(images[j], imgClip, perImage, i * 100 + j);
                        clipPaths.Add(imgClip);
                    }

                    var finalClip = Path.Combine(outputDir, $"clip_{i:D2}.mp4");
                    if (clipPaths.Count == 1)
                        File.Move(clipPaths[0], finalClip, overwrite: true);
                    else
                        await ConcatClipsAsync(clipPaths, finalClip, tempDir);

                    resolved.Add(seg with { ClipPath = finalClip });
                    Console.WriteLine($"done ({images.Count} image{(images.Count > 1 ? "s" : "")})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"failed ({ex.Message})");
                    resolved.Add(seg with { ClipPath = null });
                }
                finally
                {
                    try { Directory.Delete(tempDir, recursive: true); } catch { }
                }
            }
        }
        finally
        {
            try { Directory.Delete(poolDir, recursive: true); } catch { }
        }

        return resolved;
    }

    // ── Image pool fetching ────────────────────────────────────────────────────

    /// <summary>
    /// Fetches a pool of <paramref name="needed"/> unique images for <paramref name="query"/>.
    /// Uses multiple Bing pages + query variants to get enough distinct images.
    /// Falls back to Wikimedia if Bing comes up short.
    /// </summary>
    private static async Task<List<string>> FetchImagePoolAsync(
        string query, int needed, string poolDir, HashSet<string> usedUrls)
    {
        var collected = new List<string>();
        int dlIdx     = 0;
        // Fetch 4× what we need to give room for download failures
        int target    = needed * 4;

        // A: Bing — multiple pages and query variants for variety
        var bingUrls = new List<string>();
        foreach (var bq in BuildBingQueries(query))
        {
            // Three pages per variant: offsets 1, 31, 61
            foreach (int offset in new[] { 1, 31, 61 })
            {
                var page = await SearchBingImageUrlsAsync(bq, 30, offset);
                foreach (var u in page)
                    if (!bingUrls.Contains(u) && !usedUrls.Contains(u))
                        bingUrls.Add(u);
            }
        }

        foreach (var url in bingUrls)
        {
            if (collected.Count >= target) break;
            if (usedUrls.Contains(url)) continue;
            var dest   = Path.Combine(poolDir, $"dl_{dlIdx++:D3}.jpg");
            var result = await TryDownloadImageAsync(url, dest);
            if (result.HasValue)
            {
                usedUrls.Add(url);
                collected.Add(result.Value.path);
            }
        }

        // B: Wikimedia fallback if Bing didn't fill the pool
        if (collected.Count < needed)
        {
            var wmResults = await SearchWikimediaAsync(query, target);
            foreach (var (url, w, h, sz) in wmResults)
            {
                if (collected.Count >= target) break;
                if (usedUrls.Contains(url)) continue;
                var dest   = Path.Combine(poolDir, $"dl_{dlIdx++:D3}.jpg");
                var result = await TryDownloadImageAsync(url, dest);
                if (result.HasValue)
                {
                    usedUrls.Add(url);
                    collected.Add(result.Value.path);
                }
            }
        }

        return collected;
    }

    private static async Task<List<(string url, int w, int h, long size)>> SearchWikimediaAsync(
        string query, int maxResults)
    {
        var q   = Uri.EscapeDataString(query);
        var url = $"https://commons.wikimedia.org/w/api.php?action=query&generator=search" +
                  $"&gsrsearch={q}&gsrnamespace=6&gsrlimit=30" +
                  $"&prop=imageinfo&iiprop=url|thumburl|size|mime&iiurlwidth=1920&format=json";
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", "CarFacts/1.0 (educational project)");
            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return [];

            using var doc  = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("query", out var qElem)) return [];

            var results = new List<(string, int, int, long)>();
            foreach (var page in qElem.GetProperty("pages").EnumerateObject())
            {
                if (!page.Value.TryGetProperty("imageinfo", out var infos)) continue;
                foreach (var ii in infos.EnumerateArray())
                {
                    var mime = ii.TryGetProperty("mime", out var m) ? m.GetString() : "";
                    if (mime != "image/jpeg") continue;
                    var w    = ii.TryGetProperty("width",    out var ww) ? ww.GetInt32() : 0;
                    var h    = ii.TryGetProperty("height",   out var hh) ? hh.GetInt32() : 0;
                    var sz   = ii.TryGetProperty("size",     out var ss) ? ss.GetInt64() : 0;
                    var imgUrl = ii.TryGetProperty("thumburl", out var tu) ? tu.GetString() :
                                 ii.TryGetProperty("url",      out var uu) ? uu.GetString() : null;
                    if (imgUrl == null || w < 800 || h < 500 || h > w) continue;
                    results.Add((imgUrl, w, h, sz));
                }
            }
            return results.OrderByDescending(x => x.Item4).Take(maxResults).ToList();
        }
        catch { return []; }
    }

    private static IEnumerable<string> BuildBingQueries(string query)
    {
        var neg = "-drawing -illustration -render -CGI -artwork -sketch" +
                  " -bicycle -cycle -bike -rusty -rusted -barn -junkyard" +
                  " -abandoned -damaged -wreck -wrecked -scrap" +
                  " -house -home -building -architecture -landscape -aerial -footage -logo -poster";
        yield return $"\"{query}\" {neg}";
        yield return $"\"{query}\" automobile photograph {neg}";
        yield return $"\"{query}\" vintage car {neg}";
        yield return $"\"{query}\" car show museum {neg}";
        yield return $"{query} {neg}";    // unquoted broadener
    }

    private static async Task<List<string>> SearchBingImageUrlsAsync(string query, int maxResults, int first = 1)
    {
        var url = $"https://www.bing.com/images/search?q={Uri.EscapeDataString(query)}&first={first}&count={maxResults}" +
                  "&mkt=en-US&safeSearch=Off&qft=+filterui:imagesize-large+filterui:photo-photo";
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Accept", "text/html,application/xhtml+xml");
            req.Headers.Add("Referer", "https://www.bing.com/");
            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return [];

            var html = await resp.Content.ReadAsStringAsync();

            // Bing HTML-encodes its JSON: murl&quot;:&quot;URL&quot;
            return Regex.Matches(html, @"murl&quot;:&quot;(https?://[^&]+)", RegexOptions.IgnoreCase)
                .Select(m => System.Web.HttpUtility.HtmlDecode(m.Groups[1].Value))
                .Where(u => !string.IsNullOrEmpty(u) && !u.Contains("bing.com") && !u.Contains("bing.net"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .ToList();
        }
        catch { return []; }
    }

    private static async Task<(string path, long size)?> TryDownloadImageAsync(string imageUrl, string dest)
    {
        try
        {
            using var resp = await Http.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode) return null;

            // Accept if URL looks like JPEG or content-type is image/* (CDNs often return octet-stream)
            var ct  = resp.Content.Headers.ContentType?.MediaType ?? "";
            var ext = Path.GetExtension(new Uri(imageUrl).AbsolutePath).ToLowerInvariant();
            bool looksLikeJpeg = ct.Contains("jpeg") || ct.Contains("jpg")
                               || ext is ".jpg" or ".jpeg"
                               || ct.Contains("octet-stream") || ct.Contains("image/");
            if (!looksLikeJpeg) return null;

            await using (var stream = await resp.Content.ReadAsStreamAsync())
            await using (var file   = File.Create(dest))
                await stream.CopyToAsync(file);

            var fi = new FileInfo(dest);
            if (fi.Length < MinFileSizeBytes) { File.Delete(dest); return null; }

            // Verify JPEG magic bytes (FF D8) — filters out HTML error pages
            var header = new byte[2];
            await using var fs = File.OpenRead(dest);
            if (await fs.ReadAsync(header) < 2 || header[0] != 0xFF || header[1] != 0xD8)
            { File.Delete(dest); return null; }

            return (dest, fi.Length);
        }
        catch { return null; }
    }

    // ── Ken Burns ────────────────────────────────────────────────────────────

    private async Task MakeKenBurnsClipAsync(string imagePath, string outputPath, double duration, int index)
    {
        int    frames = Math.Max(1, (int)(duration * 30) - 1);
        // Use on-based (frame number) expressions for deterministic, drift-free zoom.
        string zExpr  = index % 2 == 0
            ? "min(1+0.0006*on,1.08)"      // zoom in:  1.0 → 1.08
            : "max(1.08-0.0006*on,1.0)";   // zoom out: 1.08 → 1.0

        string vf = string.Join(",",
            // For widescreen images: fit to width (1440), letterbox with black bars top/bottom.
            // For portrait images:   fit to height (2560), pad sides if needed.
            "scale=w='if(gt(iw/ih,9.0/16.0),1440,-2)':h='if(gt(iw/ih,9.0/16.0),-2,2560)':flags=lanczos",
            "pad=1440:2560:(ow-iw)/2:(oh-ih)/2:color=black",
            $"zoompan=z='{zExpr}':x='(iw-iw/zoom)/2':y='(ih-ih/zoom)/2':d={frames}:s=720x1280:fps=30",
            "setsar=1",
            "eq=contrast=1.12:saturation=1.35:brightness=0.02:gamma=0.96");

        var psi = new ProcessStartInfo
        {
            FileName              = ffmpegPath,
            UseShellExecute       = false,
            RedirectStandardError = true,
        };
        foreach (var arg in new[]
        {
            "-y", "-loop", "1", "-framerate", "30", "-i", imagePath,
            "-vf", vf,
            "-t", duration.ToString("F3"),
            "-frames:v", frames.ToString(),
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "24",
            "-pix_fmt", "yuv420p", "-an", outputPath
        }) psi.ArgumentList.Add(arg);

        using var proc   = Process.Start(psi)!;
        var       stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"Ken Burns ffmpeg failed (exit {proc.ExitCode}): {stderr[..Math.Min(300, stderr.Length)]}");
    }

    private async Task ConcatClipsAsync(List<string> clips, string output, string tempDir)
    {
        var listPath = Path.Combine(tempDir, "concat.txt");
        await File.WriteAllLinesAsync(listPath, clips.Select(c => $"file '{c.Replace("\\", "/")}'"));

        var psi = new ProcessStartInfo
        {
            FileName              = ffmpegPath,
            UseShellExecute       = false,
            RedirectStandardError = true,
        };
        foreach (var arg in new[]
        {
            "-y", "-f", "concat", "-safe", "0", "-i", listPath,
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "24",
            "-vsync", "cfr", "-r", "30", "-pix_fmt", "yuv420p", "-an", output
        }) psi.ArgumentList.Add(arg);

        using var proc   = Process.Start(psi)!;
        var       stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"Concat ffmpeg failed (exit {proc.ExitCode}): {stderr[..Math.Min(300, stderr.Length)]}");
    }
}
