using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Activity that stores a generated tweet reply in the Cosmos social-media-queue.
/// Hard-coded to Twitter/X since replies are platform-specific.
/// </summary>
public sealed class StoreTweetReplyQueueActivity
{
    private readonly ISocialMediaQueueStore _queueStore;
    private readonly ILogger<StoreTweetReplyQueueActivity> _logger;

    public StoreTweetReplyQueueActivity(
        ISocialMediaQueueStore queueStore,
        ILogger<StoreTweetReplyQueueActivity> logger)
    {
        _queueStore = queueStore;
        _logger = logger;
    }

    [Function(nameof(StoreTweetReplyQueueActivity))]
    public async Task<bool> Run(
        [ActivityTrigger] TweetReplyResult input)
    {
        var item = new SocialMediaQueueItem
        {
            Platform = "Twitter/X",
            Content = input.ReplyText,
            Type = "reply",
            Activity = "reply",
            ReplyToTweetId = input.TweetId
        };

        await _queueStore.AddItemsAsync([item]);

        _logger.LogInformation(
            "Stored tweet reply in queue — replying to @{Author} (tweet {TweetId})",
            input.AuthorUsername,
            input.TweetId);

        return true;
    }
}
