using CarFacts.Functions.Functions.Activities;
using CarFacts.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions;

/// <summary>
/// Orchestrator that generates social media content and queues it for scheduled posting.
/// Generates 5 standalone fact tweets + 1 blog post link tweet per enabled platform.
/// </summary>
public static class SocialMediaOrchestrator
{
    [Function(nameof(SocialMediaOrchestrator))]
    public static async Task Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(SocialMediaOrchestrator));
        var input = context.GetInput<SocialMediaOrchestratorInput>()
            ?? throw new InvalidOperationException("SocialMediaOrchestrator requires input");

        logger.LogInformation("Starting social media content generation for: {Title}", input.PostTitle);

        // Step 1: Generate 5 standalone tweet facts and blog post link tweet in parallel
        var factsTask = context.CallActivityAsync<List<TweetFactResult>>(
            nameof(GenerateTweetFactsActivity),
            "generate");

        var linkTask = context.CallActivityAsync<TweetLinkResult?>(
            nameof(GenerateTweetLinkActivity),
            new GenerateTweetLinkInput
            {
                PostUrl = input.PostUrl,
                PostTitle = input.PostTitle
            });

        await Task.WhenAll(factsTask, linkTask);

        var facts = factsTask.Result;
        var linkTweet = linkTask.Result;

        logger.LogInformation("Generated {FactCount} tweet facts + {LinkCount} link tweet",
            facts.Count, linkTweet != null ? 1 : 0);

        // Step 2: Get enabled platforms and store all items in the queue
        var enabledPlatforms = await context.CallActivityAsync<List<string>>(
            nameof(GetEnabledPlatformsActivity),
            "check");

        if (enabledPlatforms.Count == 0)
        {
            logger.LogWarning("No social media platforms enabled — skipping queue storage");
            return;
        }

        await context.CallActivityAsync<bool>(
            nameof(StoreSocialMediaQueueActivity),
            new StoreSocialQueueInput
            {
                Facts = facts,
                LinkTweet = linkTweet,
                EnabledPlatforms = enabledPlatforms
            });

        logger.LogInformation("Social media content queued for {Count} platform(s): {Platforms}",
            enabledPlatforms.Count, string.Join(", ", enabledPlatforms));
    }
}
