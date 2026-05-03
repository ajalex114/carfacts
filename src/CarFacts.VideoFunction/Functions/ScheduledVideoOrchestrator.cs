using CarFacts.VideoFunction.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Functions;

/// <summary>
/// Per-slot sub-orchestration started by DailySchedulerOrchestrator.
///
/// 1. Uses a Durable Timer to sleep until the configured <see cref="ScheduledVideoOrchestratorInput.ScheduledAt"/> time.
/// 2. Calls VideoOrchestrator as a sub-orchestration to generate and publish the video.
///
/// Returns the YouTube (or other platform) video URL on success, or null on failure.
/// </summary>
public class ScheduledVideoOrchestrator
{
    [Function(nameof(ScheduledVideoOrchestrator))]
    public async Task<string?> Run([OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var logger = ctx.CreateReplaySafeLogger<ScheduledVideoOrchestrator>();
        var input  = ctx.GetInput<ScheduledVideoOrchestratorInput>()
            ?? throw new InvalidOperationException("ScheduledVideoOrchestrator: missing input");

        var scheduledAt = DateTimeOffset.Parse(input.ScheduledAt);

        logger.LogInformation("[{Platform}] Slot {EntryId}: waiting until {ScheduledAt} UTC",
            input.Platform, input.ScheduleEntryId, scheduledAt);

        // Durable timer — zero resource consumption while waiting (SDK takes DateTime UTC)
        await ctx.CreateTimer(scheduledAt.UtcDateTime, CancellationToken.None);

        logger.LogInformation("[{Platform}] Slot {EntryId}: timer fired, starting VideoOrchestrator",
            input.Platform, input.ScheduleEntryId);

        var jobId = ctx.NewGuid().ToString("N")[..16];

        var orchestratorInput = new OrchestratorInput(
            JobId:                   jobId,
            Fact:                    null,
            StorageConnectionString: input.StorageConnectionString,
            ImageSearchQuery:        null,
            Platform:                input.Platform,
            VideoLengthSecMin:       input.VideoLengthSecMin,
            VideoLengthSecMax:       input.VideoLengthSecMax,
            NarrationStyle:          input.NarrationStyle);

        var result = await ctx.CallSubOrchestratorAsync<RenderActivityResult>(
            nameof(VideoOrchestrator),
            orchestratorInput);

        var videoUrl = result?.YouTubeVideoUrl ?? result?.VideoUrl;

        logger.LogInformation("[{Platform}] Slot {EntryId}: completed → {Url}",
            input.Platform, input.ScheduleEntryId, videoUrl ?? "(no url)");

        return videoUrl;
    }
}
