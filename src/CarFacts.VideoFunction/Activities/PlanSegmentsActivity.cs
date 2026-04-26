using CarFacts.VideoFunction.Models;
using CarFacts.VideoFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Activities;

/// <summary>
/// Activity 2 — Splits word timings into segments with Pexels search queries.
/// Pure CPU work, no I/O — runs fast.
/// </summary>
public class PlanSegmentsActivity(ILogger<PlanSegmentsActivity> logger)
{
    [Function(nameof(PlanSegmentsActivity))]
    public Task<List<VideoSegment>> Run(
        [ActivityTrigger] PlanActivityInput input,
        FunctionContext ctx)
    {
        var segments = SegmentPlanner.Plan(input.Words, input.TotalDuration, input.Fact);
        logger.LogInformation("PlanSegments: planned {Count} segments", segments.Count);
        return Task.FromResult(segments);
    }
}
