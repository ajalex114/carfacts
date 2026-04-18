using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Activity that reads all pending scheduled items from the Cosmos social-media-queue.
/// Called by ScheduledPostingOrchestrator (orchestrators cannot do I/O directly).
/// </summary>
public sealed class GetPendingScheduledItemsActivity
{
    private readonly ISocialMediaQueueStore _queueStore;
    private readonly ILogger<GetPendingScheduledItemsActivity> _logger;

    public GetPendingScheduledItemsActivity(
        ISocialMediaQueueStore queueStore,
        ILogger<GetPendingScheduledItemsActivity> logger)
    {
        _queueStore = queueStore;
        _logger = logger;
    }

    [Function(nameof(GetPendingScheduledItemsActivity))]
    public async Task<List<ScheduledPostInput>> Run(
        [ActivityTrigger] string trigger)
    {
        var items = await _queueStore.GetPendingScheduledItemsAsync();

        var result = items
            .Where(i => i.ScheduledAtUtc.HasValue)
            .Select(i => new ScheduledPostInput
            {
                ItemId = i.Id,
                Platform = i.Platform,
                Content = i.Content,
                Type = i.Type,
                Activity = i.Activity,
                PostUrl = i.PostUrl,
                PostTitle = i.PostTitle,
                ReplyToTweetId = i.ReplyToTweetId,
                ScheduledAtUtc = i.ScheduledAtUtc!.Value
            })
            .ToList();

        _logger.LogInformation("Found {Count} scheduled items to process", result.Count);
        return result;
    }
}
