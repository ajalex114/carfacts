using CarFacts.Functions.Functions.Activities;
using CarFacts.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions;

/// <summary>
/// Simple orchestrator that posts one item from the queue per enabled platform.
/// Triggered by SocialMediaPostingTrigger every 4 hours.
/// </summary>
public static class SocialMediaPostingOrchestrator
{
    private static readonly RetryPolicy RetryPolicy = new(
        maxNumberOfAttempts: 2,
        firstRetryInterval: TimeSpan.FromSeconds(10));

    [Function(nameof(SocialMediaPostingOrchestrator))]
    public static async Task Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(SocialMediaPostingOrchestrator));

        // Get enabled platforms
        var platforms = await context.CallActivityAsync<List<string>>(
            nameof(GetEnabledPlatformsActivity),
            "check");

        if (platforms.Count == 0)
        {
            logger.LogInformation("No social media platforms enabled — skipping posting");
            return;
        }

        // Post one item per platform in parallel
        var tasks = platforms.Select(platform =>
            context.CallActivityAsync<bool>(
                nameof(PostFromQueueActivity),
                new PostFromQueueInput { Platform = platform },
                new TaskOptions(RetryPolicy)));

        var results = await Task.WhenAll(tasks);

        var posted = results.Count(r => r);
        logger.LogInformation("Social media posting complete: {Posted}/{Total} platforms had content",
            posted, platforms.Count);
    }
}
