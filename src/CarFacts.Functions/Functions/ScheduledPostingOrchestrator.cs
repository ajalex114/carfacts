using CarFacts.Functions.Functions.Activities;
using CarFacts.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions;

/// <summary>
/// Orchestrator that reads all pending scheduled items from Cosmos and starts
/// an independent ScheduledPostOrchestrator instance for each one.
/// Each instance sleeps until its scheduled time then fires — fully isolated.
/// </summary>
public static class ScheduledPostingOrchestrator
{
    [Function(nameof(ScheduledPostingOrchestrator))]
    public static async Task Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(ScheduledPostingOrchestrator));

        logger.LogInformation("Starting scheduled posting orchestrator — reading pending items");

        // Step 1: Read all pending scheduled items from Cosmos (via activity)
        var items = await context.CallActivityAsync<List<ScheduledPostInput>>(
            nameof(GetPendingScheduledItemsActivity),
            "read");

        if (items.Count == 0)
        {
            logger.LogInformation("No scheduled items found — nothing to schedule");
            return;
        }

        logger.LogInformation("Scheduling {Count} posts across times: {Times}",
            items.Count,
            string.Join(", ", items.Select(i => i.ScheduledAtUtc.ToString("HH:mm 'UTC'"))));

        // Step 2: Start an independent orchestrator instance per item
        var tasks = items.Select(item =>
            context.CallSubOrchestratorAsync(
                nameof(ScheduledPostOrchestrator),
                item,
                new TaskOptions(new RetryPolicy(maxNumberOfAttempts: 1, firstRetryInterval: TimeSpan.FromSeconds(5)))));

        await Task.WhenAll(tasks);

        logger.LogInformation("All {Count} scheduled posts completed", items.Count);
    }
}
