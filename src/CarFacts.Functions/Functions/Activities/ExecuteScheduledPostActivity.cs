using CarFacts.Functions.Models;
using CarFacts.Functions.Services;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Activity that publishes a scheduled social media post, removes it from the queue,
/// and increments social counts for link-type posts.
/// Called by ScheduledPostOrchestrator after the durable timer fires.
/// </summary>
public sealed class ExecuteScheduledPostActivity
{
    private readonly SocialMediaPublisher _publisher;
    private readonly ISocialMediaQueueStore _queueStore;
    private readonly IFactKeywordStore _factStore;
    private readonly ILogger<ExecuteScheduledPostActivity> _logger;

    public ExecuteScheduledPostActivity(
        SocialMediaPublisher publisher,
        ISocialMediaQueueStore queueStore,
        IFactKeywordStore factStore,
        ILogger<ExecuteScheduledPostActivity> logger)
    {
        _publisher = publisher;
        _queueStore = queueStore;
        _factStore = factStore;
        _logger = logger;
    }

    [Function(nameof(ExecuteScheduledPostActivity))]
    public async Task<bool> Run(
        [ActivityTrigger] ScheduledPostInput input)
    {
        _logger.LogInformation(
            "Executing scheduled post at {Time} for {Platform} [{Type}]: {ItemId}",
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
            input.Platform,
            input.Type,
            input.ItemId);

        await _publisher.PublishRawAsync(input.Platform, input.Content);

        await _queueStore.DeleteItemAsync(input.ItemId, input.Platform);

        if (input.Type == "link" && !string.IsNullOrEmpty(input.PostUrl))
        {
            await _factStore.IncrementSocialCountsAsync(input.PostUrl, input.Platform);
            _logger.LogInformation("Incremented social counts for {PostUrl} on {Platform}", input.PostUrl, input.Platform);
        }

        _logger.LogInformation("Posted {Type} to {Platform}: {Id}", input.Type, input.Platform, input.ItemId);
        return true;
    }
}
