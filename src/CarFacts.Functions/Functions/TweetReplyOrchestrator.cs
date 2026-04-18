using CarFacts.Functions.Functions.Activities;
using CarFacts.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions;

/// <summary>
/// Orchestrator that generates a tweet reply (search → AI → store in queue).
/// The reply is stored in Cosmos with activity="reply" for the scheduling pipeline to execute.
/// </summary>
public static class TweetReplyOrchestrator
{
    [Function(nameof(TweetReplyOrchestrator))]
    public static async Task Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(TweetReplyOrchestrator));

        logger.LogInformation("Starting tweet reply generation");

        // Step 1: Search Twitter + generate reply
        var replyResult = await context.CallActivityAsync<TweetReplyResult>(
            nameof(GenerateTweetReplyActivity),
            "generate");

        logger.LogInformation(
            "Generated reply to @{Author} (tweet {TweetId}): {Reply}",
            replyResult.AuthorUsername,
            replyResult.TweetId,
            replyResult.ReplyText);

        // Step 2: Store in queue
        await context.CallActivityAsync<bool>(
            nameof(StoreTweetReplyQueueActivity),
            replyResult);

        logger.LogInformation("Tweet reply queued for posting");
    }
}
