using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CarFacts.VideoFunction.Models;
using CarFacts.VideoFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Activities;

/// <summary>
/// Activity 4 — Downloads clips + audio from blob, runs FFmpeg to render the final video,
/// uploads output.mp4 to poc-videos, returns a 48h SAS URL.
/// Gets its own 10-minute activity budget — no HTTP timeout pressure.
/// </summary>
public class RenderVideoActivity(
    FfmpegManager ffmpegManager,
    ILogger<RenderVideoActivity> logger)
{
    [Function(nameof(RenderVideoActivity))]
    public async Task<RenderActivityResult> Run(
        [ActivityTrigger] RenderActivityInput input,
        FunctionContext ctx)
    {
        logger.LogInformation("[{JobId}] RenderVideo: {ClipCount} clips, {Duration:F1}s",
            input.JobId, input.ClipUrls.Count(u => u != null), input.TotalDuration);

        var ffmpegPath = await ffmpegManager.EnsureReadyAsync();
        var tempDir    = Path.Combine(Path.GetTempPath(), $"render-{input.JobId}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // ── 1. Download audio ────────────────────────────────────────────
            var audioPath = Path.Combine(tempDir, "narration.wav");
            await DownloadAsync(input.AudioUrl, audioPath);

            // ── 2. Write subtitles file (no BOM — libass can choke on UTF-8 BOM) ──
            var subtitlePath = Path.Combine(tempDir, "subtitles.ass");
            await File.WriteAllTextAsync(subtitlePath, input.AssSubtitleText,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            // ── 3. Download trimmed clips (paired with their actual durations) ──
            // Pair each URL+duration, skip null entries (clips that failed to fetch)
            var validPairs = input.ClipUrls
                .Select((url, i) => new {
                    Url      = url,
                    Duration = input.SegmentDurations != null && i < input.SegmentDurations.Count
                               ? input.SegmentDurations[i]
                               : input.TotalDuration / Math.Max(input.ClipUrls.Count(u => u != null), 1)
                })
                .Where(p => p.Url != null)
                .ToList();

            var clipPaths     = new List<string>();
            var clipDurations = new List<double>();
            for (int i = 0; i < validPairs.Count; i++)
            {
                var clipPath = Path.Combine(tempDir, $"clip_{i:D2}.mp4");
                await DownloadAsync(validPairs[i].Url!, clipPath);
                clipPaths.Add(clipPath);
                clipDurations.Add(validPairs[i].Duration);
            }

            if (clipPaths.Count == 0)
                throw new InvalidOperationException("No clips available to render.");

            logger.LogInformation("[{JobId}] Downloaded {Count} clips", input.JobId, clipPaths.Count);

            // ── 4. Build VideoSegment list with local paths + actual durations ─
            // Use cumulative real durations so xfade offsets are correct.
            double cumStart = 0;
            var segments = clipPaths.Select((p, i) =>
            {
                var dur = clipDurations[i];
                var seg = new VideoSegment(
                    SearchQuery:  $"clip_{i}",
                    StartSeconds: cumStart,
                    EndSeconds:   cumStart + dur)
                    with { ClipPath = p };
                cumStart += dur;
                return seg;
            }).ToList();

            // ── 5. Render ────────────────────────────────────────────────────
            var outputPath = Path.Combine(tempDir, "output.mp4");
            var generator  = new VideoGenerator(ffmpegPath);
            await generator.GenerateFromClipsAsync(
                segments, audioPath, subtitlePath, null, outputPath, input.TotalDuration);

            var fileSize = new FileInfo(outputPath).Length;
            logger.LogInformation("[{JobId}] Render complete: {MB:F1} MB", input.JobId, fileSize / 1024.0 / 1024.0);

            // ── 6. Upload final video to poc-videos ──────────────────────────
            var blobName = $"carfact-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{input.JobId[..8]}.mp4";
            var videoUrl = await UploadVideoAsync(input.StorageConnectionString, outputPath, blobName);

            logger.LogInformation("[{JobId}] Uploaded video: {Url}", input.JobId, videoUrl[..80]);
            return new RenderActivityResult(videoUrl, input.TotalDuration, clipPaths.Count, input.ClipSources);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static readonly System.Net.Http.HttpClient Http = new();

    private static async Task DownloadAsync(string url, string destPath)
    {
        using var resp = await Http.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync();
        await using var file   = File.Create(destPath);
        await stream.CopyToAsync(file);
    }

    private static async Task<string> UploadVideoAsync(
        string connectionString, string filePath, string blobName)
    {
        var container = new BlobContainerClient(connectionString, "poc-videos");
        await container.CreateIfNotExistsAsync(PublicAccessType.None);

        var blob = container.GetBlobClient(blobName);
        await using var stream = File.OpenRead(filePath);
        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "video/mp4" }
        });

        var sasUri = blob.GenerateSasUri(
            Azure.Storage.Sas.BlobSasPermissions.Read,
            DateTimeOffset.UtcNow.AddHours(48));
        return sasUri.ToString();
    }
}
