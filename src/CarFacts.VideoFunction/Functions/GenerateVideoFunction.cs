using CarFacts.VideoFunction.Models;
using CarFacts.VideoFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace CarFacts.VideoFunction.Functions;

public class GenerateVideoFunction(
    FfmpegManager ffmpegManager,
    TtsService ttsService,
    SubtitleGenerator subtitleGenerator,
    VideoStorageService storageService,
    PexelsApiKeyHolder pexelsKey,
    ILogger<GenerateVideoFunction> logger)
{
    private const string DefaultFact =
        "In 1908, Henry Ford introduced the Model T — " +
        "the first car built for ordinary people. " +
        "Ford painted every one black, " +
        "because black paint dried the fastest, keeping the assembly line moving. " +
        "At peak production, a new Model T rolled off the line every 24 seconds. " +
        "It didn't just change driving. It changed the world.";

    [Function("GenerateVideo")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", "get")] HttpRequestData req,
        FunctionContext ctx)
    {
        string fact = DefaultFact;
        try
        {
            var body = await req.ReadFromJsonAsync<GenerateRequest>();
            if (!string.IsNullOrWhiteSpace(body?.Fact))
                fact = body.Fact;
        }
        catch { /* use default */ }

        logger.LogInformation("GenerateVideo triggered. Fact: {Fact}", fact[..Math.Min(60, fact.Length)]);

        // ── Ensure FFmpeg is ready (downloads on first cold start) ──────────
        var ffmpegPath = await ffmpegManager.EnsureReadyAsync();
        logger.LogInformation("FFmpeg ready at {Path}", ffmpegPath);

        // ── Isolated temp directory per invocation ──────────────────────────
        var tempDir = Path.Combine(Path.GetTempPath(), $"poc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        logger.LogInformation("Temp dir: {Dir}", tempDir);

        try
        {
            // ── 1. TTS → WAV + word timings ─────────────────────────────────
            var audioPath = Path.Combine(tempDir, "narration.wav");
            logger.LogInformation("Synthesizing TTS...");
            var words = await ttsService.SynthesizeAsync(fact, audioPath);
            logger.LogInformation("TTS done: {Count} words", words.Count);

            // ── 2. Duration ─────────────────────────────────────────────────
            var narrationEnd  = words[^1].EndSeconds;
            var totalDuration = narrationEnd + 2.3;

            // ── 3. ASS subtitles ────────────────────────────────────────────
            var subtitlePath = Path.Combine(tempDir, "subtitles.ass");
            var assText = subtitleGenerator.GenerateAss(words, totalDuration, "carfactsdaily.com");
            await File.WriteAllTextAsync(subtitlePath, assText, System.Text.Encoding.UTF8);

            // ── 4. Segment planner ──────────────────────────────────────────
            var segments = SegmentPlanner.Plan(words, totalDuration, fact);
            logger.LogInformation("Planned {Count} segments", segments.Count);

            // ── 5. Fetch Pexels clips ────────────────────────────────────────
            // Trimmed clips cached in Temp (keyed by query+duration), source files deleted immediately
            var cacheDir = Path.Combine(Path.GetTempPath(), "poc-clips-cache");
            var pexels   = new PexelsVideoService(pexelsKey.ApiKey, ffmpegPath, cacheDir);
            var resolved = await pexels.ResolveClipsAsync(segments, tempDir);
            var readyClips = resolved.Where(s => s.ClipPath is not null).ToList();
            logger.LogInformation("Clips ready: {Count}/{Total}", readyClips.Count, segments.Count);

            // ── 6. Render ────────────────────────────────────────────────────
            var outputPath = Path.Combine(tempDir, "output.mp4");
            var generator  = new VideoGenerator(ffmpegPath);

            if (readyClips.Count > 0)
                await generator.GenerateFromClipsAsync(readyClips, audioPath, "subtitles.ass", null, outputPath, totalDuration);
            else
                throw new InvalidOperationException("No Pexels clips resolved — cannot render.");

            logger.LogInformation("Render complete: {Bytes} bytes", new FileInfo(outputPath).Length);

            // ── 7. Upload to blob ────────────────────────────────────────────
            var blobName  = $"carfact-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N[..6]}.mp4";
            var videoUrl  = await storageService.UploadAsync(outputPath, blobName);
            logger.LogInformation("Uploaded: {Url}", videoUrl);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                videoUrl,
                durationSeconds = Math.Round(totalDuration, 1),
                wordCount       = words.Count,
                clipCount       = readyClips.Count,
                fact            = fact[..Math.Min(80, fact.Length)] + (fact.Length > 80 ? "…" : "")
            });
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Video generation failed");
            var err = req.CreateResponse(HttpStatusCode.InternalServerError);
            await err.WriteAsJsonAsync(new { error = ex.Message });
            return err;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
        }
    }
}

public record GenerateRequest(string? Fact);
