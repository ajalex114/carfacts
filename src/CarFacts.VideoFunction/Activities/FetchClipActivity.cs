using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CarFacts.VideoFunction.Models;
using CarFacts.VideoFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CarFacts.VideoFunction.Activities;

/// <summary>
/// Activity 3 — Handles exactly ONE clip:
///   1. Try YouTube CC (brand-aware, shot-type-filtered, watermark+car checks via CV)
///   2. Fall back to Pexels if YouTube fails or finds no clean candidate
///   3. Trim with ffmpeg → upload trimmed clip to blob → return SAS URL + attribution
/// Fan-out: orchestrator fires one instance per segment, all running in parallel.
/// </summary>
public class FetchClipActivity(
    FfmpegManager ffmpegManager,
    YtDlpManager ytDlpManager,
    ILogger<FetchClipActivity> logger)
{
    private static readonly HttpClient Http = new();

    [Function(nameof(FetchClipActivity))]
    public async Task<FetchClipActivityResult> Run(
        [ActivityTrigger] FetchClipActivityInput input,
        FunctionContext ctx)
    {
        logger.LogInformation("[{JobId}] FetchClip[{Index}] [{Shot}]: query='{Query}'",
            input.JobId, input.Index, input.ShotType, input.SearchQuery);

        var ffmpegPath = await ffmpegManager.EnsureReadyAsync();
        var tempDir    = Path.Combine(Path.GetTempPath(), $"clip-{input.JobId}-{input.Index}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var trimmed = Path.Combine(tempDir, "clip.mp4");
            string? attribution = null;

            // ── Attempt 1: YouTube CC ────────────────────────────────────────
            if (!string.IsNullOrEmpty(input.YouTubeApiKey))
            {
                try
                {
                    var ytDlpPath   = await ytDlpManager.EnsureReadyAsync();
                    var cookiesPath = await ytDlpManager.EnsureCookiesAsync();
                    var visionSvc   = new ComputerVisionService(input.VisionEndpoint, input.VisionApiKey);
                    var youtubeSvc  = new YouTubeVideoService(input.YouTubeApiKey, ytDlpPath, visionSvc, cookiesPath, ffmpegPath, input.ProxyUrl);

                    var sourceTmp   = Path.Combine(tempDir, "yt-source.mp4");
                    attribution     = await youtubeSvc.FetchClipAsync(input.SearchQuery, input.Duration + 1.5, sourceTmp);

                    if (attribution != null && File.Exists(sourceTmp))
                    {
                        await TrimClipAsync(ffmpegPath, sourceTmp, trimmed, input.Duration);
                        logger.LogInformation("[{JobId}] FetchClip[{Index}]: used YouTube CC — {Attr}",
                            input.JobId, input.Index, attribution);
                    }
                    else
                    {
                        attribution = null;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning("[{JobId}] FetchClip[{Index}]: YouTube attempt failed: {Msg}",
                        input.JobId, input.Index, ex.Message);
                    attribution = null;
                }
            }

            // ── Attempt 2: Pexels fallback ───────────────────────────────────
            if (!File.Exists(trimmed))
            {
                logger.LogInformation("[{JobId}] FetchClip[{Index}]: falling back to Pexels",
                    input.JobId, input.Index);

                var sourceTmp = Path.Combine(tempDir, "pexels-source.mp4");

                // Three-tier fallback: model-specific → brand fallback → generic car
                var videoUrl = await SearchPexelsWithFallbackAsync(
                    input.SearchQuery,
                    input.FallbackQuery,
                    input.BrandOnlyFallback,
                    input.PexelsApiKey,
                    logger, input.JobId, input.Index);

                using var resp = await Http.GetAsync(videoUrl, HttpCompletionOption.ResponseHeadersRead);
                resp.EnsureSuccessStatusCode();
                await using (var stream = await resp.Content.ReadAsStreamAsync())
                await using (var file   = File.Create(sourceTmp))
                    await stream.CopyToAsync(file);

                await TrimClipAsync(ffmpegPath, sourceTmp, trimmed, input.Duration);
            }

            // ── Upload to blob ───────────────────────────────────────────────
            var blobPath = $"poc-jobs/{input.JobId}/clip_{input.Index:D2}.mp4";
            var clipUrl  = await UploadClipAsync(input.StorageConnectionString, trimmed, blobPath);

            logger.LogInformation("[{JobId}] FetchClip[{Index}]: done → {Url}",
                input.JobId, input.Index, clipUrl[..60]);
            return new FetchClipActivityResult(input.Index, clipUrl, attribution);
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

    // ── Pexels search ────────────────────────────────────────────────────────

    private static async Task<string> SearchPexelsAsync(string query, string apiKey)
    {
        var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.pexels.com/videos/search?query={Uri.EscapeDataString(query)}&per_page=15&orientation=portrait");
        req.Headers.Authorization = new AuthenticationHeaderValue(apiKey);

        var resp   = await Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<PexelsSearchResult>()
            ?? throw new InvalidOperationException("Empty Pexels response");

        // Score candidates: prefer portrait HD clips in the 5–60s range
        var best = result.Videos?
            .Where(v => v.Height > v.Width)   // portrait only
            .Select(v => (Video: v, Score: ScorePexelsVideo(v)))
            .OrderByDescending(x => x.Score)
            .FirstOrDefault().Video
            ?? result.Videos?.FirstOrDefault()
            ?? throw new InvalidOperationException($"No Pexels results for '{query}'");

        var file = best.VideoFiles?
            .Where(f => f.FileType == "video/mp4" && f.Width >= 540)
            .OrderBy(f => f.Width * f.Height)   // smallest that meets min quality — saves disk on Consumption plan
            .FirstOrDefault()
            ?? best.VideoFiles?.Where(f => f.FileType == "video/mp4")
                   .OrderBy(f => f.Width * f.Height).FirstOrDefault()
            ?? throw new InvalidOperationException("No MP4 found");

        return file.Link ?? throw new InvalidOperationException("Pexels link is null");
    }

    /// <summary>
    /// Scores a Pexels video for suitability as a car b-roll clip.
    /// Prefers portrait HD clips in the 5–60 second range.
    /// </summary>
    private static int ScorePexelsVideo(PexelsVideo v)
    {
        int score = 0;

        // Duration sweet spot: 5–60 seconds of usable footage
        if (v.Duration >= 5 && v.Duration <= 60) score += 3;
        else if (v.Duration > 60 && v.Duration <= 120) score += 1;

        // Resolution quality
        var bestFile = v.VideoFiles?
            .Where(f => f.FileType == "video/mp4")
            .OrderByDescending(f => f.Width * f.Height)
            .FirstOrDefault();
        if (bestFile?.Height >= 1080) score += 3;
        else if (bestFile?.Height >= 720) score += 2;
        else if (bestFile?.Height >= 480) score += 1;

        return score;
    }

    private static async Task<string> SearchPexelsWithFallbackAsync(
        string primaryQuery, string? fallbackQuery, string? brandOnlyFallback, string apiKey,
        ILogger logger, string jobId, int index)
    {
        // Tier 1: model-specific query (e.g. "Ford Mustang exterior rolling b-roll footage")
        try { return await SearchPexelsAsync(primaryQuery, apiKey); }
        catch (Exception ex)
        {
            logger.LogWarning("[{JobId}] FetchClip[{Index}]: primary Pexels query failed ({Msg}), trying fallback",
                jobId, index, ex.Message);
        }

        // Tier 2: brand fallback (e.g. "Ford Mustang car driving road footage")
        if (!string.IsNullOrEmpty(fallbackQuery))
        {
            try { return await SearchPexelsAsync(fallbackQuery, apiKey); }
            catch (Exception ex)
            {
                logger.LogWarning("[{JobId}] FetchClip[{Index}]: fallback Pexels query failed ({Msg}), trying brand-only",
                    jobId, index, ex.Message);
            }
        }

        // Tier 3: brand-only (e.g. "Ford car driving road footage") — most likely to have results
        if (!string.IsNullOrEmpty(brandOnlyFallback))
        {
            try { return await SearchPexelsAsync(brandOnlyFallback, apiKey); }
            catch (Exception ex)
            {
                logger.LogWarning("[{JobId}] FetchClip[{Index}]: brand-only Pexels query failed ({Msg}), trying generic",
                    jobId, index, ex.Message);
            }
        }

        // Tier 4: absolute last resort — specific enough to avoid bikes/motorcycles
        return await SearchPexelsAsync("luxury car driving highway cinematic", apiKey);
    }

    // ── FFmpeg trim ──────────────────────────────────────────────────────────

    private static async Task TrimClipAsync(string ffmpegPath, string source, string output, double duration)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName              = ffmpegPath,
            UseShellExecute       = false,
            RedirectStandardError = true,
        };
        foreach (var arg in new[]
        {
            "-y", "-ss", "0", "-i", source,
            "-t", duration.ToString("F3"),
            "-vf", "scale=720:1280:force_original_aspect_ratio=increase,crop=720:1280,setsar=1",
            "-r", "30",
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "24",
            "-an", output
        }) psi.ArgumentList.Add(arg);

        using var proc = System.Diagnostics.Process.Start(psi)!;
        await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg trim failed (exit {proc.ExitCode})");
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

        var sasUri = blob.GenerateSasUri(
            Azure.Storage.Sas.BlobSasPermissions.Read,
            DateTimeOffset.UtcNow.AddHours(4));
        return sasUri.ToString();
    }

    // ── JSON models (Pexels) ─────────────────────────────────────────────────

    private record PexelsSearchResult(
        [property: JsonPropertyName("videos")] List<PexelsVideo>? Videos);

    private record PexelsVideo(
        [property: JsonPropertyName("id")]          int Id,
        [property: JsonPropertyName("width")]       int Width,
        [property: JsonPropertyName("height")]      int Height,
        [property: JsonPropertyName("duration")]    int Duration,
        [property: JsonPropertyName("video_files")] List<PexelsVideoFile>? VideoFiles);

    private record PexelsVideoFile(
        [property: JsonPropertyName("link")]      string? Link,
        [property: JsonPropertyName("width")]     int Width,
        [property: JsonPropertyName("height")]    int Height,
        [property: JsonPropertyName("file_type")] string? FileType);
}
