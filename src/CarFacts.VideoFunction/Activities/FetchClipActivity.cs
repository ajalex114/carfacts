using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CarFacts.VideoFunction.Models;
using CarFacts.VideoFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.RegularExpressions;

namespace CarFacts.VideoFunction.Activities;

/// <summary>
/// Activity 3 — Handles exactly ONE video segment:
///   1. Build Bing image search queries from the LLM-provided imageSearchQuery
///   2. Scrape Bing Images for high-quality JPEG URLs
///   3. Download and filter by file size (≥100 KB, JPEG content-type)
///   4. Apply Ken Burns zoompan effect via ffmpeg (1.5s per image, alternating zoom-in/out)
///   5. Concatenate image clips to match the segment duration
///   6. Upload result to blob storage → return SAS URL
/// Fan-out: orchestrator fires one instance per segment, all running in parallel.
/// </summary>
public class FetchClipActivity(
    FfmpegManager ffmpegManager,
    ILogger<FetchClipActivity> logger)
{
    private static readonly HttpClient Http;
    private static int _cleanupDone = 0;

    private const double ClipImageDuration = 1.5;
    private const long   MinFileSizeBytes  = 250_000;  // 250 KB — wallpaper-grade quality gate
    private const int    MaxImagesPerQuery = 10;

    static FetchClipActivity()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect         = true,
            MaxAutomaticRedirections  = 5,
            AutomaticDecompression    = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        Http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        Http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        Http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    }

    [Function(nameof(FetchClipActivity))]
    public async Task<FetchClipActivityResult> Run(
        [ActivityTrigger] FetchClipActivityInput input,
        FunctionContext ctx)
    {
        CleanupOldTempDirsOnce(input.JobId, logger);

        var ffmpegPath = await ffmpegManager.EnsureReadyAsync();
        var tempDir    = Path.Combine(Path.GetTempPath(), $"clip-{input.JobId}-{input.Index}");
        Directory.CreateDirectory(tempDir);

        logger.LogInformation("[{JobId}] FetchClip[{Index}]: query='{Query}' duration={Dur:F1}s",
            input.JobId, input.Index, input.SearchQuery, input.Duration);

        try
        {
            const double MinImageDisplay = 1.5;
            int imagesNeeded = Math.Min(4, Math.Max(1, (int)(input.Duration / MinImageDisplay)));

            var imageFiles = await CollectImagesAsync(input.SearchQuery, imagesNeeded, tempDir, input.Index);
            if (imageFiles.Count == 0)
                throw new InvalidOperationException($"No usable images found for '{input.SearchQuery}'");

            logger.LogInformation("[{JobId}] FetchClip[{Index}]: collected {Count} images",
                input.JobId, input.Index, imageFiles.Count);

            double perImage  = input.Duration / imageFiles.Count;
            var clipPaths = new List<string>();
            for (int i = 0; i < imageFiles.Count; i++)
            {
                var clipPath = Path.Combine(tempDir, $"img_{i:D2}.mp4");
                await MakeKenBurnsClipAsync(ffmpegPath, imageFiles[i], clipPath, perImage, input.Index * 100 + i);
                clipPaths.Add(clipPath);
            }

            var outputPath = Path.Combine(tempDir, "clip.mp4");
            if (clipPaths.Count == 1)
                File.Move(clipPaths[0], outputPath);
            else
                await ConcatClipsAsync(ffmpegPath, clipPaths, outputPath, tempDir);

            var blobPath = $"poc-jobs/{input.JobId}/clip_{input.Index:D2}.mp4";
            var clipUrl  = await UploadClipAsync(input.StorageConnectionString, outputPath, blobPath);

            logger.LogInformation("[{JobId}] FetchClip[{Index}]: done → {Url}",
                input.JobId, input.Index, clipUrl[..Math.Min(80, clipUrl.Length)]);
            return new FetchClipActivityResult(input.Index, clipUrl, $"Bing: {input.SearchQuery}");
        }
        catch (Exception ex)
        {
            logger.LogWarning("[{JobId}] FetchClip[{Index}] failed: {Msg}",
                input.JobId, input.Index, ex.Message);
            return new FetchClipActivityResult(input.Index, null);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    // ── Image collection: Bing primary, Wikimedia fallback ──────────────────

    /// <summary>
    /// Primary source: Wikimedia Commons API — community-curated labels, no mislabeling.
    /// Returns image URLs sorted by file size descending.
    /// </summary>
    private static async Task<List<(string url, int width, int height, long size)>> SearchWikimediaAsync(
        string imageSearchQuery, int maxResults)
    {
        var q   = Uri.EscapeDataString(imageSearchQuery);
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
            var pages      = doc.RootElement
                .GetProperty("query").GetProperty("pages").EnumerateObject();

            var results = new List<(string url, int w, int h, long size)>();
            foreach (var page in pages)
            {
                if (!page.Value.TryGetProperty("imageinfo", out var infos)) continue;
                foreach (var ii in infos.EnumerateArray())
                {
                    var mime   = ii.TryGetProperty("mime",     out var m)  ? m.GetString()  : "";
                    if (mime != "image/jpeg") continue;
                    var w      = ii.TryGetProperty("width",    out var ww) ? ww.GetInt32()  : 0;
                    var h      = ii.TryGetProperty("height",   out var hh) ? hh.GetInt32()  : 0;
                    var sz     = ii.TryGetProperty("size",     out var ss) ? ss.GetInt64()  : 0;
                    // Prefer 1920px thumbnail URL — avoids 429 rate-limit on full-res downloads
                    var imgUrl = ii.TryGetProperty("thumburl", out var tu) ? tu.GetString() :
                                 ii.TryGetProperty("url",      out var uu) ? uu.GetString() : null;
                    if (imgUrl == null || w < 1200 || h < 600 || h > w) continue;
                    results.Add((imgUrl, w, h, sz));
                }
            }
            return results.OrderByDescending(x => x.size).Take(maxResults).ToList();
        }
        catch { return []; }
    }

    /// <summary>
    /// Generates Bing search queries for car images with strong photo-appropriate negatives.
    /// </summary>
    private static IEnumerable<string> BuildBingQueries(string imageSearchQuery)
    {
        var q   = $"\"{imageSearchQuery}\"";
        var neg = "-drawing -illustration -render -CGI -artwork -sketch" +
                  " -bicycle -cycle -bike -rusty -rusted -barn -junkyard" +
                  " -abandoned -damaged -wreck -wrecked -scrap" +
                  " -house -home -building -architecture -landscape -aerial -footage -logo -poster";
        yield return $"{q} {neg}";
        yield return $"{q} automobile photograph {neg}";
        yield return $"{q} vintage car {neg}";
        yield return $"{q} car show museum {neg}";
        yield return $"{imageSearchQuery} {neg}";    // unquoted broadener
    }

    /// <summary>
    /// Scrapes Bing Images HTML for direct JPEG URLs.
    /// <paramref name="first"/> is the 1-based page offset (1, 31, 61 … for pages 1–3).
    /// </summary>
    private static async Task<List<string>> SearchBingForImageUrlsAsync(string query, int maxResults, int first = 1)
    {
        var url = $"https://www.bing.com/images/search?q={Uri.EscapeDataString(query)}&first={first}&count={maxResults}&mkt=en-US&safeSearch=Off&qft=+filterui:imagesize-large+filterui:photo-photo";
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
                .Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value))
                .Where(u => !u.Contains("bing.com") && !u.Contains("bing.net"))
                .Distinct()
                .Take(maxResults)
                .ToList();
        }
        catch { return []; }
    }

    /// <summary>
    /// Downloads a single image URL, validates JPEG magic bytes + size, returns local path or null.
    /// </summary>
    private async Task<(string path, long size)?> TryDownloadImageAsync(string imageUrl, string dest)
    {
        try
        {
            using var resp = await Http.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode) return null;
            var ct = resp.Content.Headers.ContentType?.MediaType ?? "";
            // Accept if content-type looks image-like or is octet-stream (some CDNs don't set MIME correctly)
            bool mightBeJpeg = ct.Contains("jpeg") || ct.Contains("jpg")
                             || ct.Contains("octet-stream") || ct.StartsWith("image/");
            if (!mightBeJpeg) return null;

            await using (var stream = await resp.Content.ReadAsStreamAsync())
            await using (var file   = File.Create(dest))
                await stream.CopyToAsync(file);

            // Verify JPEG magic bytes (0xFF 0xD8) — rejects HTML error pages masquerading as images
            var header = new byte[2];
            await using (var check = File.OpenRead(dest))
                if (await check.ReadAsync(header) < 2 || header[0] != 0xFF || header[1] != 0xD8)
                { File.Delete(dest); return null; }

            var size = new FileInfo(dest).Length;
            if (size < MinFileSizeBytes) { File.Delete(dest); return null; }
            return (dest, size);
        }
        catch { return null; }
    }

    /// <summary>
    /// Collects images: Bing primary (3 pages × multiple queries), Wikimedia Commons as fallback.
    /// <paramref name="segmentIndex"/> offsets the Bing start page so parallel segments
    /// fetching the same query get different result pages and don't repeat images.
    /// </summary>
    private async Task<List<string>> CollectImagesAsync(string imageSearchQuery, int needed, string tempDir, int segmentIndex = 0)
    {
        var collected  = new List<(string path, long size)>();
        var seenSizes  = new HashSet<long>();
        int dlIdx      = 0;

        // ── A: Bing primary ──────────────────────────────────────────────────
        // Each segment uses a different Bing page offset to avoid duplicate results
        // across parallel activities querying the same search term.
        int startOffset = segmentIndex * 10 + 1;
        var bingUrls = new List<string>();
        foreach (var query in BuildBingQueries(imageSearchQuery))
        {
            foreach (var pageFirst in new[] { startOffset, startOffset + 30, startOffset + 60 })
            {
                var page = await SearchBingForImageUrlsAsync(query, MaxImagesPerQuery, pageFirst);
                bingUrls.AddRange(page);
            }
        }
        bingUrls = bingUrls.Distinct().ToList();
        logger.LogInformation("[Bing] '{Q}' → {N} candidate URLs", imageSearchQuery, bingUrls.Count);

        foreach (var imageUrl in bingUrls)
        {
            if (collected.Count >= needed) break;
            var dest   = Path.Combine(tempDir, $"dl_{dlIdx++:D3}.jpg");
            var result = await TryDownloadImageAsync(imageUrl, dest);
            if (result.HasValue && !seenSizes.Contains(result.Value.size))
            {
                seenSizes.Add(result.Value.size);
                collected.Add(result.Value);
            }
        }
        logger.LogInformation("[Bing] accepted {Count}/{Needed}", collected.Count, needed);

        // ── B: Wikimedia fallback ────────────────────────────────────────────
        if (collected.Count < needed)
        {
            logger.LogInformation("[Wikimedia fallback] need {More} more images", needed - collected.Count);
            var wmCandidates = await SearchWikimediaAsync(imageSearchQuery, needed * 2);
            foreach (var (imgUrl, w, h, sz) in wmCandidates)
            {
                if (collected.Count >= needed) break;
                var dest   = Path.Combine(tempDir, $"dl_{dlIdx++:D3}.jpg");
                var result = await TryDownloadImageAsync(imgUrl, dest);
                if (result.HasValue && !seenSizes.Contains(result.Value.size))
                {
                    seenSizes.Add(result.Value.size);
                    collected.Add(result.Value);
                    logger.LogInformation("[Wikimedia] ✅ {W}x{H} {KB}KB", w, h, result.Value.size / 1024);
                }
            }
        }

        return collected
            .OrderByDescending(x => x.size)
            .Take(needed)
            .Select(x => x.path)
            .ToList();
    }

    // ── FFmpeg: Ken Burns ────────────────────────────────────────────────────

    /// <summary>
    /// Produces a single Ken Burns clip from a still image.
    /// Even-indexed clips zoom in; odd-indexed clips zoom out — creates visual rhythm.
    /// </summary>
    private async Task MakeKenBurnsClipAsync(
        string ffmpegPath, string imagePath, string outputPath, double duration, int index)
    {
        int    frames = Math.Max(1, (int)(duration * 30) - 1);
        // Use on-based (frame number) expressions for deterministic, drift-free zoom.
        // State-based zoom+step accumulates float errors over frames → visible shake.
        string zExpr  = index % 2 == 0
            ? "min(1+0.0006*on,1.08)"      // zoom in:  1.0 → 1.08
            : "max(1.08-0.0006*on,1.0)";   // zoom out: 1.08 → 1.0

        string vf = string.Join(",",
            // For widescreen images: fit to width (1440), letterbox with black bars top/bottom.
            // For portrait images:   fit to height (2560), pad sides if needed.
            // The threshold 9.0/16.0 = 0.5625 is the target frame aspect ratio.
            "scale=w='if(gt(iw/ih,9.0/16.0),1440,-2)':h='if(gt(iw/ih,9.0/16.0),-2,2560)':flags=lanczos",
            "pad=1440:2560:(ow-iw)/2:(oh-ih)/2:color=black",
            // (iw-iw/zoom)/2 avoids float precision jitter vs iw/2-(iw/zoom/2).
            $"zoompan=z='{zExpr}':x='(iw-iw/zoom)/2':y='(ih-ih/zoom)/2':d={frames}:s=720x1280:fps=30",
            "setsar=1",
            "eq=contrast=1.12:saturation=1.35:brightness=0.02:gamma=0.96");

        var psi = new System.Diagnostics.ProcessStartInfo
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

        using var proc   = System.Diagnostics.Process.Start(psi)!;
        var       stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"Ken Burns ffmpeg failed (exit {proc.ExitCode}): {stderr[..Math.Min(300, stderr.Length)]}");
    }

    /// <summary>
    /// Concatenates Ken Burns clips with re-encode to ensure CFR and no blank frames at cuts.
    /// </summary>
    private async Task ConcatClipsAsync(
        string ffmpegPath, List<string> clips, string output, string tempDir)
    {
        var listPath = Path.Combine(tempDir, "concat.txt");
        await File.WriteAllLinesAsync(listPath, clips.Select(c => $"file '{c.Replace("\\", "/")}'"));

        var psi = new System.Diagnostics.ProcessStartInfo
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

        using var proc   = System.Diagnostics.Process.Start(psi)!;
        var       stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"Concat ffmpeg failed (exit {proc.ExitCode}): {stderr[..Math.Min(300, stderr.Length)]}");
    }

    // ── Cleanup ──────────────────────────────────────────────────────────────

    private void CleanupOldTempDirsOnce(string currentJobId, ILogger log)
    {
        if (Interlocked.CompareExchange(ref _cleanupDone, 1, 0) != 0) return;
        try
        {
            int deleted = 0;
            foreach (var dir in Directory.GetDirectories(Path.GetTempPath(), "clip-*"))
            {
                if (dir.Contains(currentJobId)) continue;
                try
                {
                    if (new DirectoryInfo(dir).LastWriteTimeUtc < DateTime.UtcNow.AddMinutes(-5))
                    {
                        Directory.Delete(dir, recursive: true);
                        deleted++;
                    }
                }
                catch { }
            }
            if (deleted > 0)
                log.LogInformation("[{JobId}] Cleaned up {Count} orphaned clip temp dirs", currentJobId, deleted);
        }
        catch (Exception ex) { log.LogWarning("[{JobId}] Temp cleanup scan failed: {Msg}", currentJobId, ex.Message); }
    }

    // ── Blob upload ──────────────────────────────────────────────────────────

    private static async Task<string> UploadClipAsync(
        string connectionString, string filePath, string blobPath)
    {
        var parts     = blobPath.Split('/', 2);
        var container = new BlobContainerClient(connectionString, parts[0]);
        await container.CreateIfNotExistsAsync(PublicAccessType.None);

        var blob = container.GetBlobClient(parts[1]);
        await using var stream = File.OpenRead(filePath);
        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "video/mp4" }
        });

        return blob.GenerateSasUri(
            Azure.Storage.Sas.BlobSasPermissions.Read,
            DateTimeOffset.UtcNow.AddHours(4)).ToString();
    }
}

