using CarFacts.Functions.Functions.Activities;
using CarFacts.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions;

/// <summary>
/// Per-item orchestrator that sleeps until the scheduled time then executes the post.
/// Each instance handles exactly one queue item — if it fails, other items are unaffected.
/// </summary>
public static class ScheduledPostOrchestrator
{
    private static readonly RetryPolicy RetryPolicy = new(
        maxNumberOfAttempts: 2,
        firstRetryInterval: TimeSpan.FromSeconds(10));

    [Function(nameof(ScheduledPostOrchestrator))]
    public static async Task Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(ScheduledPostOrchestrator));
        var input = context.GetInput<ScheduledPostInput>()
            ?? throw new InvalidOperationException("ScheduledPostOrchestrator requires input");

        logger.LogInformation(
            "Scheduled post orchestrator started for {Platform} item {ItemId}, scheduled at {Time}",
            input.Platform, input.ItemId, input.ScheduledAtUtc.ToString("HH:mm:ss UTC"));

        // If the scheduled time is in the future, sleep until then
        if (input.ScheduledAtUtc > context.CurrentUtcDateTime)
        {
            logger.LogInformation(
                "Sleeping until {Time} ({Minutes} minutes from now)",
                input.ScheduledAtUtc.ToString("HH:mm:ss UTC"),
                (input.ScheduledAtUtc - context.CurrentUtcDateTime).TotalMinutes.ToString("F0"));

            await context.CreateTimer(input.ScheduledAtUtc, CancellationToken.None);
        }
        else
        {
            logger.LogWarning(
                "Scheduled time {Time} is in the past — executing immediately",
                input.ScheduledAtUtc.ToString("HH:mm:ss UTC"));
        }

        // Timer fired (or was already past) — execute the post
        await context.CallActivityAsync<bool>(
            nameof(ExecuteScheduledPostActivity),
            input,
            new TaskOptions(RetryPolicy));

        logger.LogInformation("Scheduled post completed for {Platform} item {ItemId}", input.Platform, input.ItemId);
    }
}
