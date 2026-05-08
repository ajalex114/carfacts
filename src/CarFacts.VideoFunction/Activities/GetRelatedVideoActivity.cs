using CarFacts.VideoFunction.Models;
using CarFacts.VideoFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Activities;

/// <summary>
/// Activity 4.5 — Queries Cosmos DB for the best "Related Video" to include
/// as a backlink in the new video's YouTube description.
/// Selection logic: prefer videos at least 5 days old with the fewest backlinks.
/// Falls back to the oldest video with fewest backlinks if no 5-day-old video exists.
/// Non-fatal: returns empty result if Cosmos is unavailable.
/// </summary>
public class GetRelatedVideoActivity(
    VideoTrackingService trackingService,
    ILogger<GetRelatedVideoActivity> logger)
{
    [Function(nameof(GetRelatedVideoActivity))]
    public async Task<GetRelatedVideoActivityResult> Run(
        [ActivityTrigger] GetRelatedVideoActivityInput input,
        FunctionContext ctx)
    {
        var related = await trackingService.GetRelatedVideoForBacklinkAsync(input.Platform);
        var relatedUrl = related?.PlatformVideoUrl;
        if (relatedUrl != null)
            logger.LogInformation("[{JobId}] Related video ({Platform}): {Brand} → {Url} (backlinks={Count})",
                input.JobId, input.Platform, related!.Brand, relatedUrl, related.BacklinkCount);
        else
            logger.LogInformation("[{JobId}] No related video available yet for platform {Platform}",
                input.JobId, input.Platform);

        return new GetRelatedVideoActivityResult(related?.Id, related?.Brand, relatedUrl);
    }
}
