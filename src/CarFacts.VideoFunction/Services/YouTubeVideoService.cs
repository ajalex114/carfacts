using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CarFacts.VideoFunction.Services;

/// <summary>
/// Searches YouTube for Creative Commons licensed videos and downloads a short clip.
/// 
/// Pipeline per clip:
///   1. YouTube Data API search with videoLicense=creativeCommon (Layer 1: CC-only)
///   2. Score + rank results by title keywords (Layer 2: no reviews/vlogs)
///   3. Azure Computer Vision thumbnail check (Layer 3: no watermarks + car present)
///   4. yt-dlp downloads just the segment we need (no full video)
/// </summary>
public class YouTubeVideoService(
    string youTubeApiKey,
    string ytDlpPath,
    ComputerVisionService visionService,
    string? cookiesPath = null,
    string? ffmpegPath  = null)
{
    private static readonly HttpClient Http = new();

    // Skip videos whose titles suggest talking-head, review, or text-heavy content
    private static readonly string[] TitleSkipTerms =
    [
        "review", "test drive", "how to", "tutorial", "reaction", "vlog",
        "episode", "unboxing", "my new", "i bought", "comparison", "walkaround",
        "walk around", "why i", "should you", "is it worth", "first drive",
        "part 1", "part 2", "part 3", "ep.", "ep ", "#", "podcast",
    ];

    // Prefer videos whose titles suggest clean stock/cinematic footage
    private static readonly string[] TitlePreferTerms =
    [
        "footage", "b-roll", "b roll", "stock", "4k", "cinematic",
        "driving footage", "car video", "timelapse", "time lapse",
    ];

    public record YouTubeClip(
        string VideoId,
        string Title,
        string ChannelTitle,
        string Attribution);

    /// <summary>
    /// Searches for a CC-licensed car video matching the query, applies all 3 filter layers,
    /// then downloads the first N seconds using yt-dlp.
    /// Returns null if no suitable clip found (caller should fall back to Pexels).
    /// </summary>
    public async Task<string?> FetchClipAsync(string query, double duration, string outputPath)
    {
        var candidate = await FindBestCandidateAsync(query);
        if (candidate is null)
        {
            Console.WriteLine($"  ℹ️  No YouTube CC candidate for '{query}' — will use fallback");
            return null;
        }

        Console.WriteLine($"  ▶️  YouTube [{candidate.VideoId}] \"{candidate.Title}\"");

        var ok = await DownloadClipAsync(candidate.VideoId, duration, outputPath);
        return ok ? candidate.Attribution : null;
    }

    private async Task<YouTubeClip?> FindBestCandidateAsync(string query)
    {
        var results = await SearchAsync(query);
        if (results.Count == 0) return null;

        // Score + sort by title quality
        var scored = results
            .Select(r => (Result: r, Score: ScoreTitle(r.Title)))
            .Where(x => x.Score >= 0)   // negative score = hard skip
            .OrderByDescending(x => x.Score)
            .ToList();

        // Check top 5 via thumbnail analysis (watermark + car presence)
        foreach (var (result, _) in scored.Take(5))
        {
            var analysis = await visionService.AnalyzeThumbnailAsync(result.VideoId);
            if (analysis.HasWatermark)
            {
                Console.WriteLine($"  🚫 Watermark detected: {result.VideoId} \"{result.Title}\"");
                continue;
            }
            if (!analysis.HasCar)
            {
                Console.WriteLine($"  🚫 No car in thumbnail: {result.VideoId} \"{result.Title}\"");
                continue;
            }

            return new YouTubeClip(
                result.VideoId,
                result.Title,
                result.ChannelTitle,
                $"\"{result.Title}\" by {result.ChannelTitle} / CC BY 3.0");
        }

        return null;
    }

    private async Task<List<SearchResultItem>> SearchAsync(string query)
    {
        try
        {
            var url = $"https://www.googleapis.com/youtube/v3/search" +
                      $"?part=snippet" +
                      $"&q={Uri.EscapeDataString(query)}" +
                      $"&type=video" +
                      $"&videoLicense=creativeCommon" +
                      $"&videoDefinition=high" +
                      $"&videoDuration=short" +   // under 4 minutes — avoid long compilations
                      $"&maxResults=10" +
                      $"&key={youTubeApiKey}";

            var resp = await Http.GetFromJsonAsync<YouTubeSearchResponse>(url);
            return resp?.Items?
                .Select(i => new SearchResultItem(
                    i.Id?.VideoId ?? "",
                    i.Snippet?.Title ?? "",
                    i.Snippet?.ChannelTitle ?? ""))
                .Where(r => !string.IsNullOrEmpty(r.VideoId))
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  YouTube search failed for '{query}': {ex.Message}");
            return [];
        }
    }

    private static int ScoreTitle(string title)
    {
        var lower = title.ToLowerInvariant();

        // Hard skip
        if (TitleSkipTerms.Any(t => lower.Contains(t)))
            return -1;

        // Prefer bonus
        int score = 0;
        foreach (var t in TitlePreferTerms)
            if (lower.Contains(t)) score += 2;

        return score;
    }

    /// <summary>
    /// Uses yt-dlp to download just the first `duration` seconds of a YouTube video.
    /// yt-dlp --download-sections "*0-{duration}" selects only the needed segment.
    /// Remuxes to mp4 to ensure ffmpeg compatibility regardless of source codec.
    /// </summary>
    private async Task<bool> DownloadClipAsync(string videoId, double duration, string outputPath)
    {
        try
        {
            var sectionArg = $"*0-{(int)Math.Ceiling(duration + 1)}"; // slight buffer

            // yt-dlp may append its own extension — use a template and find the result after
            var outputDir      = Path.GetDirectoryName(outputPath)!;
            var outputBaseName = Path.GetFileNameWithoutExtension(outputPath);
            var outputTemplate = Path.Combine(outputDir, outputBaseName + ".%(ext)s");

            var psi = new ProcessStartInfo
            {
                FileName              = ytDlpPath,
                UseShellExecute       = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            // Use a shared PyInstaller extraction dir so parallel instances don't each
            // extract ~200MB of Python runtime — they will share the same cached dir.
            var extractCache = Path.Combine(Path.GetTempPath(), "ytdlp-pyinstaller-cache");
            Directory.CreateDirectory(extractCache);
            psi.EnvironmentVariables["PYINSTALLER_TMPDIR"] = extractCache;

            foreach (var arg in new[]
            {
                $"https://www.youtube.com/watch?v={videoId}",
                "--download-sections", sectionArg,
                "--format", "bestvideo[height<=720][ext=mp4]/bestvideo[height<=720][ext=webm]/bestvideo[height<=720]/bestvideo/best[height<=720]/best",
                "--remux-video", "mp4",     // ensure mp4 output regardless of source codec
                "--no-check-formats",       // skip ffprobe format validation — saves 96MB disk on Consumption plan
                "--no-playlist",
                "--no-warnings",
                // Try multiple player clients — some bypass bot detection differently on datacenter IPs
                // mweb = mobile web, tv_embedded = TV embedded player (no sign-in required on some IPs)
                "--extractor-args", "youtube:player_client=mweb,tv_embedded,ios,android",
                "--output", outputTemplate,
            }) psi.ArgumentList.Add(arg);

            // Tell yt-dlp where to find ffmpeg — required for --download-sections and --remux-video
            var resolvedFfmpeg = ffmpegPath ?? Path.Combine(Path.GetTempPath(), "poc-ffmpeg-bin", "ffmpeg.exe");
            if (File.Exists(resolvedFfmpeg))
            {
                psi.ArgumentList.Add("--ffmpeg-location");
                psi.ArgumentList.Add(Path.GetDirectoryName(resolvedFfmpeg)!);
            }

            // Use cookies if available — bypasses bot detection on Azure datacenter IPs
            if (!string.IsNullOrEmpty(cookiesPath) && File.Exists(cookiesPath))
            {
                psi.ArgumentList.Add("--cookies");
                psi.ArgumentList.Add(cookiesPath);
            }

            using var proc = Process.Start(psi)!;
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            // Find the output file — yt-dlp may have named it with .mp4, .webm, etc.
            var resultFile = Directory.GetFiles(outputDir, $"{outputBaseName}.*")
                                      .FirstOrDefault();

            if (proc.ExitCode != 0 || resultFile == null)
            {
                Console.WriteLine($"  ⚠️  yt-dlp failed (exit {proc.ExitCode}): {stderr[..Math.Min(200, stderr.Length)]}");
                return false;
            }

            // Rename to the expected path if yt-dlp added a different extension
            if (!string.Equals(resultFile, outputPath, StringComparison.OrdinalIgnoreCase))
                File.Move(resultFile, outputPath, overwrite: true);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  yt-dlp exception: {ex.Message}");
            return false;
        }
    }

    // ── JSON models ──────────────────────────────────────────────────────────

    private record SearchResultItem(string VideoId, string Title, string ChannelTitle);

    private record YouTubeSearchResponse(
        [property: JsonPropertyName("items")] List<YouTubeSearchItem>? Items);

    private record YouTubeSearchItem(
        [property: JsonPropertyName("id")]      YouTubeVideoId?   Id,
        [property: JsonPropertyName("snippet")] YouTubeSnippet?   Snippet);

    private record YouTubeVideoId(
        [property: JsonPropertyName("videoId")] string? VideoId);

    private record YouTubeSnippet(
        [property: JsonPropertyName("title")]        string? Title,
        [property: JsonPropertyName("channelTitle")] string? ChannelTitle);
}
