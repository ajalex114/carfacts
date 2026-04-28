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
///   1. Pick a random scenic road/travel query
///   2. Search Pexels for that query (portrait orientation)
///   3. Download the video, apply 2× speed via ffmpeg, trim to required duration
///   4. Upload trimmed clip to blob → return SAS URL
/// Fan-out: orchestrator fires one instance per segment, all running in parallel.
/// </summary>
public class FetchClipActivity(
    FfmpegManager ffmpegManager,
    YtDlpManager ytDlpManager,   // kept for DI compatibility — not used in scenic mode
    ILogger<FetchClipActivity> logger)
{
    private static readonly HttpClient Http = new();
    private static int _cleanupDone = 0;

    // Scenic road/travel queries — one is chosen at random per clip
    private static readonly string[] ScenicQueries =
    [
        "exotic road trip scenic route",
        "mountain road aerial view travel",
        "island road trip drone shot",
        "switzerland scenic road trip drone",
        "iceland ring road drone",
    ];

    [Function(nameof(FetchClipActivity))]
    public async Task<FetchClipActivityResult> Run(
        [ActivityTrigger] FetchClipActivityInput input,
        FunctionContext ctx)
    {
        CleanupOldTempDirsOnce(input.JobId, logger);

        var ffmpegPath = await ffmpegManager.EnsureReadyAsync();
        var tempDir    = Path.Combine(Path.GetTempPath(), $"clip-{input.JobId}-{input.Index}");
        Directory.CreateDirectory(tempDir);

        // Each clip index gets a deterministic-but-varied scenic query
        var query = ScenicQueries[input.Index % ScenicQueries.Length];
        logger.LogInformation("[{JobId}] FetchClip[{Index}]: scenic query='{Query}' duration={Dur:F1}s",
            input.JobId, input.Index, query, input.Duration);

        try
        {
            var trimmed   = Path.Combine(tempDir, "clip.mp4");
            var sourceTmp = Path.Combine(tempDir, "pexels-source.mp4");

            // Search Pexels for the scenic query
            var videoUrl = await SearchPexelsAsync(query, input.PexelsApiKey);

            // Download
            using var resp = await Http.GetAsync(videoUrl, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            await using (var stream = await resp.Content.ReadAsStreamAsync())
            await using (var file   = File.Create(sourceTmp))
                await stream.CopyToAsync(file);

            var sizeMb = new FileInfo(sourceTmp).Length / 1024.0 / 1024.0;
            logger.LogInformation("[{JobId}] FetchClip[{Index}]: downloaded {MB:F1} MB", input.JobId, input.Index, sizeMb);

            // Trim + 2× speed: we need `duration` seconds of output;
            // at 2× speed that requires `duration * 2` seconds of source footage.
            await TrimClipAt2xAsync(ffmpegPath, sourceTmp, trimmed, input.Duration);

            // ── Upload to blob ───────────────────────────────────────────────
            var blobPath = $"poc-jobs/{input.JobId}/clip_{input.Index:D2}.mp4";
            var clipUrl  = await UploadClipAsync(input.StorageConnectionString, trimmed, blobPath);

            logger.LogInformation("[{JobId}] FetchClip[{Index}]: done (scenic/{Query}) → {Url}",
                input.JobId, input.Index, query, clipUrl[..60]);
            return new FetchClipActivityResult(input.Index, clipUrl, $"Pexels scenic: {query}");
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
            $"https://api.pexels.com/videos/search?query={Uri.EscapeDataString(query)}&per_page=10&orientation=portrait");
        req.Headers.Authorization = new AuthenticationHeaderValue(apiKey);

        var resp   = await Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<PexelsSearchResult>()
            ?? throw new InvalidOperationException("Empty Pexels response");

        // Pick the best portrait video — prefer 30–60s so at 2× it's 15–30s per clip
        var best = result.Videos?
            .Where(v => v.Height > v.Width && v.Duration >= 10)
            .OrderByDescending(v => v.Duration >= 30 && v.Duration <= 90 ? 1 : 0)
            .FirstOrDefault()
            ?? result.Videos?.FirstOrDefault()
            ?? throw new InvalidOperationException($"No Pexels results for '{query}'");

        // Prefer 540–720px wide files (safe disk size on Consumption plan)
        var file = best.VideoFiles?
            .Where(f => f.FileType == "video/mp4" && f.Width >= 540 && f.Width <= 720)
            .OrderBy(f => f.Width * f.Height)
            .FirstOrDefault()
            ?? best.VideoFiles?.Where(f => f.FileType == "video/mp4" && f.Width >= 540)
                   .OrderBy(f => f.Width * f.Height).FirstOrDefault()
            ?? best.VideoFiles?.Where(f => f.FileType == "video/mp4")
                   .OrderBy(f => f.Width * f.Height).FirstOrDefault()
            ?? throw new InvalidOperationException("No MP4 found");

        return file.Link ?? throw new InvalidOperationException("Pexels link is null");
    }

    /// <summary>
    /// Cleans up orphaned clip temp dirs from previous failed runs.
    /// Runs at most once per cold start to reclaim disk space on Consumption plan.
    /// </summary>
    private void CleanupOldTempDirsOnce(string currentJobId, ILogger log)
    {
        if (Interlocked.CompareExchange(ref _cleanupDone, 1, 0) != 0) return;

        try
        {
            var tmp = Path.GetTempPath();
            int deleted = 0;
            foreach (var dir in Directory.GetDirectories(tmp, "clip-*"))
            {
                if (dir.Contains(currentJobId)) continue;
                try
                {
                    var info = new DirectoryInfo(dir);
                    if (info.LastWriteTimeUtc < DateTime.UtcNow.AddMinutes(-5))
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
        catch (Exception ex)
        {
            log.LogWarning("[{JobId}] Temp cleanup scan failed: {Msg}", currentJobId, ex.Message);
        }
    }

    // ── FFmpeg: trim + 2× speed ──────────────────────────────────────────────

    /// <summary>
    /// Produces exactly <paramref name="outputDuration"/> seconds of output at 2× speed.
    /// Takes <c>outputDuration × 2</c> seconds of source, then applies setpts=0.5*PTS.
    /// </summary>
    private static async Task TrimClipAt2xAsync(string ffmpegPath, string source, string output, double outputDuration)
    {
        var rawDuration = outputDuration * 2 + 1; // extra second buffer

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName              = ffmpegPath,
            UseShellExecute       = false,
            RedirectStandardError = true,
        };
        foreach (var arg in new[]
        {
            "-y", "-ss", "0", "-i", source,
            "-t", rawDuration.ToString("F3"),
            "-vf", $"scale=720:1280:force_original_aspect_ratio=increase,crop=720:1280,setsar=1,setpts=0.5*PTS",
            "-r", "30",
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "24",
            "-an", output
        }) psi.ArgumentList.Add(arg);

        using var proc = System.Diagnostics.Process.Start(psi)!;
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg 2x trim failed (exit {proc.ExitCode}): {stderr[..Math.Min(200, stderr.Length)]}");
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
