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

        // If this is a reply placeholder (no content yet), generate the reply first
        // Use retry loop since some tweets may have conversation restrictions or content filter issues
        if (input.Activity == "reply" && string.IsNullOrEmpty(input.ReplyToTweetId))
        {
            const int maxAttempts = 3;
            logger.LogInformation("Reply placeholder detected — generating reply via Twitter search + AI (up to {Max} attempts)", maxAttempts);

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var replyResult = await context.CallActivityAsync<TweetReplyResult>(
                        nameof(GenerateTweetReplyActivity),
                        $"generate-attempt-{attempt}");

                    input.Content = replyResult.ReplyText;
                    input.ReplyToTweetId = replyResult.TweetId;

                    logger.LogInformation(
                        "Attempt {Attempt}: Generated reply to @{Author} (tweet {TweetId}): {Reply}",
                        attempt,
                        replyResult.AuthorUsername,
                        replyResult.TweetId,
                        replyResult.ReplyText);

                    await context.CallActivityAsync<bool>(
                        nameof(ExecuteScheduledPostActivity),
                        input,
                        new TaskOptions(new RetryPolicy(maxNumberOfAttempts: 1, firstRetryInterval: TimeSpan.FromSeconds(5))));

                    logger.LogInformation("Scheduled reply completed for {Platform} item {ItemId}", input.Platform, input.ItemId);
                    return;
                }
                catch (TaskFailedException ex) when (attempt < maxAttempts)
                {
                    logger.LogWarning(
                        "Reply attempt {Attempt} failed: {Error}. Trying again...",
                        attempt,
                        ex.Message);
                }
            }

            // All retries exhausted — delete the placeholder so it doesn't pile up
            logger.LogWarning("All {Max} reply attempts failed — cleaning up placeholder item {ItemId}", maxAttempts, input.ItemId);
            return;
        }

        // If this is a like placeholder, find a tweet to like first
        if (input.Activity == "like" && string.IsNullOrEmpty(input.ReplyToTweetId))
        {
            const int maxAttempts = 3;
            logger.LogInformation("Like placeholder detected — finding a car tweet to like (up to {Max} attempts)", maxAttempts);

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var likeResult = await context.CallActivityAsync<TweetLikeResult>(
                        nameof(GenerateTweetLikeActivity),
                        $"like-attempt-{attempt}");

                    // Reuse ReplyToTweetId to carry the target tweet ID
                    input.ReplyToTweetId = likeResult.TweetId;
                    input.Content = likeResult.Text;

                    logger.LogInformation(
                        "Attempt {Attempt}: Selected tweet to like from @{Author} (tweet {TweetId})",
                        attempt,
                        likeResult.AuthorUsername,
                        likeResult.TweetId);

                    await context.CallActivityAsync<bool>(
                        nameof(ExecuteScheduledPostActivity),
                        input,
                        new TaskOptions(new RetryPolicy(maxNumberOfAttempts: 1, firstRetryInterval: TimeSpan.FromSeconds(5))));

                    logger.LogInformation("Scheduled like completed for {Platform} item {ItemId}", input.Platform, input.ItemId);
                    return;
                }
                catch (TaskFailedException ex) when (attempt < maxAttempts)
                {
                    logger.LogWarning(
                        "Like attempt {Attempt} failed: {Error}. Trying again...",
                        attempt,
                        ex.Message);
                }
            }

            logger.LogWarning("All {Max} like attempts failed — cleaning up placeholder item {ItemId}", maxAttempts, input.ItemId);
            return;
        }

        // Timer fired (or was already past) — execute the post
        await context.CallActivityAsync<bool>(
            nameof(ExecuteScheduledPostActivity),
            input,
            new TaskOptions(RetryPolicy));

        logger.LogInformation("Scheduled post completed for {Platform} item {ItemId}", input.Platform, input.ItemId);
    }
}
