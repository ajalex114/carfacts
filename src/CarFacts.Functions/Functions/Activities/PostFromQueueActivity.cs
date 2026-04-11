using CarFacts.Functions.Models;
using CarFacts.Functions.Services;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Functions.Activities;

public sealed class PostFromQueueActivity
{
    private readonly ISocialMediaQueueStore _queueStore;
    private readonly IFactKeywordStore _factStore;
    private readonly SocialMediaPublisher _publisher;
    private readonly ILogger<PostFromQueueActivity> _logger;

    public PostFromQueueActivity(
        ISocialMediaQueueStore queueStore,
        IFactKeywordStore factStore,
        SocialMediaPublisher publisher,
        ILogger<PostFromQueueActivity> logger)
    {
        _queueStore = queueStore;
        _factStore = factStore;
        _publisher = publisher;
        _logger = logger;
    }

    [Function(nameof(PostFromQueueActivity))]
    public async Task<bool> Run(
        [ActivityTrigger] PostFromQueueInput input)
    {
        _logger.LogInformation("Posting from queue for platform: {Platform}", input.Platform);

        var item = await _queueStore.GetRandomItemAsync(input.Platform);
        if (item == null)
        {
            _logger.LogInformation("No items in queue for {Platform} — skipping", input.Platform);
            return false;
        }

        // Post via the existing social media publisher (which routes to TwitterService etc.)
        // The content is already fully formatted with hashtags and URL
        await _publisher.PublishRawAsync(input.Platform, item.Content);

        // Delete from queue after successful posting
        await _queueStore.DeleteItemAsync(item.Id, item.Platform);

        // If this was a link tweet, increment the social media counts
        if (item.Type == "link" && !string.IsNullOrEmpty(item.PostUrl))
        {
            await _factStore.IncrementSocialCountsAsync(item.PostUrl, input.Platform);
            _logger.LogInformation("Incremented social counts for {PostUrl} on {Platform}", item.PostUrl, input.Platform);
        }

        _logger.LogInformation("Posted {Type} to {Platform}: {Id}", item.Type, input.Platform, item.Id);
        return true;
    }
}
