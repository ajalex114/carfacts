using CarFacts.VideoFunction.Activities;
using CarFacts.VideoFunction.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Functions;

/// <summary>
/// Durable orchestrator — chains all video generation activities.
/// The client gets a 202 immediately; this runs async without any HTTP timeout.
/// Fan-out: one FetchClipActivity per segment, all running in parallel.
/// </summary>
public class VideoOrchestrator
{
    [Function(nameof(VideoOrchestrator))]
    public async Task<RenderActivityResult> Run(
        [OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var logger = ctx.CreateReplaySafeLogger<VideoOrchestrator>();
        var input  = ctx.GetInput<OrchestratorInput>()
            ?? throw new InvalidOperationException("Missing orchestrator input");

        logger.LogInformation("[{JobId}] Orchestrator started", input.JobId);

        // ── Step 0: Generate car fact (if not provided by caller) ────────────
        string fact;
        if (!string.IsNullOrWhiteSpace(input.Fact))
        {
            fact = input.Fact;
            logger.LogInformation("[{JobId}] Step 0: Using caller-provided fact", input.JobId);
        }
        else
        {
            logger.LogInformation("[{JobId}] Step 0: GenerateCarFact (LLM)", input.JobId);
            var factResult = await ctx.CallActivityAsync<GenerateCarFactActivityResult>(
                nameof(GenerateCarFactActivity),
                new GenerateCarFactActivityInput(input.JobId));
            fact = factResult.Fact;
            logger.LogInformation("[{JobId}] Generated fact: \"{Preview}\"",
                input.JobId, fact[..Math.Min(80, fact.Length)]);
        }

        // ── Step 1: TTS + subtitles ──────────────────────────────────────────
        logger.LogInformation("[{JobId}] Step 1: SynthesizeTts", input.JobId);
        var ttsResult = await ctx.CallActivityAsync<TtsActivityResult>(
            nameof(SynthesizeTtsActivity),
            new TtsActivityInput(input.JobId, fact)
            {
                StorageConnectionString = input.StorageConnectionString
            });

        // ── Step 1.5: Generate image search query ─────────────────────────────
        logger.LogInformation("[{JobId}] Step 1.5: GenerateSearchQuery", input.JobId);
        var queryResult = await ctx.CallActivityAsync<GenerateQueryActivityResult>(
            nameof(GenerateSearchQueryActivity),
            new GenerateQueryActivityInput(input.JobId, fact));
        var effectiveQuery = input.ImageSearchQuery ?? queryResult.Query;
        logger.LogInformation("[{JobId}] Image search query: \"{Query}\"", input.JobId, effectiveQuery);

        // ── Step 2: Plan segments ────────────────────────────────────────────
        logger.LogInformation("[{JobId}] Step 2: PlanSegments ({Words} words)", input.JobId, ttsResult.Words.Count);
        var segments = await ctx.CallActivityAsync<List<VideoSegment>>(
            nameof(PlanSegmentsActivity),
            new PlanActivityInput(ttsResult.Words, ttsResult.TotalDuration, fact, effectiveQuery));

        // ── Step 3: Fan-out — fetch each clip in parallel ────────────────────
        logger.LogInformation("[{JobId}] Step 3: FetchClips (fan-out × {Count})", input.JobId, segments.Count);
        var clipTasks = segments.Select((seg, i) =>
            ctx.CallActivityAsync<FetchClipActivityResult>(
                nameof(FetchClipActivity),
                new FetchClipActivityInput(
                    JobId:                      input.JobId,
                    Index:                      i,
                    SearchQuery:                seg.SearchQuery,
                    Duration:                   seg.EndSeconds - seg.StartSeconds,
                    StorageConnectionString:    input.StorageConnectionString,
                    FfmpegBlobConnectionString: input.StorageConnectionString,
                    ShotType:                   seg.ShotType,
                    FallbackQuery:              seg.FallbackQuery,
                    BrandOnlyFallback:          seg.BrandOnlyFallback)));

        var clipResults = await Task.WhenAll(clipTasks);
        var orderedResults = clipResults.OrderBy(r => r.Index).ToList();
        var clipUrls       = orderedResults.Select(r => r.ClipUrl).ToList();
        var clipDurations  = orderedResults.Select(r => segments[r.Index].Duration).ToList();
        var readyCount  = clipUrls.Count(u => u != null);

        // Build per-clip source summary for status API
        var clipSources = orderedResults.Select((r, i) =>
        {
            var seg = segments[r.Index];
            return new ClipSource(r.Index, "Bing/Wikimedia", seg.SearchQuery);
        }).ToList();

        logger.LogInformation("[{JobId}] Clips ready: {Ready}/{Total}", input.JobId, readyCount, segments.Count);

        if (readyCount == 0)
            throw new InvalidOperationException("No clips resolved — cannot render.");

        // ── Step 4: Render ────────────────────────────────────────────────────
        logger.LogInformation("[{JobId}] Step 4: RenderVideo", input.JobId);
        var result = await ctx.CallActivityAsync<RenderActivityResult>(
            nameof(RenderVideoActivity),
            new RenderActivityInput(
                JobId:                   input.JobId,
                AudioUrl:                ttsResult.AudioUrl,
                AssSubtitleText:         ttsResult.AssSubtitleText,
                ClipUrls:                clipUrls,
                TotalDuration:           ttsResult.TotalDuration,
                StorageConnectionString: input.StorageConnectionString,
                SegmentDurations:        clipDurations,
                ClipSources:             clipSources));

        logger.LogInformation("[{JobId}] ✅ Complete: {Url}", input.JobId, result.VideoUrl[..80]);
        return result;
    }
}

public record OrchestratorInput(
    string JobId,
    string? Fact,
    string StorageConnectionString,
    string? ImageSearchQuery = null);
