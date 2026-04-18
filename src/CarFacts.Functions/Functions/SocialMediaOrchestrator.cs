using CarFacts.Functions.Functions.Activities;
using CarFacts.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions;

/// <summary>
/// Orchestrator that generates social media content and queues it for scheduled posting.
/// Generates configurable standalone fact tweets + blog post link tweets per enabled platform.
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

        var factsPerDay = input.FactsPerDay > 0 ? input.FactsPerDay : 5;
        var linkPostsPerDay = input.LinkPostsPerDay > 0 ? input.LinkPostsPerDay : 1;

        // Step 1: Generate standalone tweet facts and blog post link tweets in parallel
        var factsTask = context.CallActivityAsync<List<TweetFactResult>>(
            nameof(GenerateTweetFactsActivity),
            factsPerDay);

        var linkTask = context.CallActivityAsync<List<TweetLinkResult>>(
            nameof(GenerateTweetLinkActivity),
            new GenerateTweetLinkInput
            {
                PostUrl = input.PostUrl,
                PostTitle = input.PostTitle,
                LinkCount = linkPostsPerDay
            });

        await Task.WhenAll(factsTask, linkTask);

        var facts = factsTask.Result;
        var linkTweets = linkTask.Result;

        logger.LogInformation("Generated {FactCount} tweet facts + {LinkCount} link tweet(s)",
            facts.Count, linkTweets.Count);

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
                LinkTweets = linkTweets,
                EnabledPlatforms = enabledPlatforms,
                LikesEnabled = input.LikesEnabled,
                RepliesEnabled = input.RepliesEnabled,
                LikesPerDayMin = input.LikesPerDayMin,
                LikesPerDayMax = input.LikesPerDayMax,
                RepliesPerDayMin = input.RepliesPerDayMin,
                RepliesPerDayMax = input.RepliesPerDayMax
            });

        logger.LogInformation("Social media content queued for {Count} platform(s): {Platforms}",
            enabledPlatforms.Count, string.Join(", ", enabledPlatforms));
    }
}
