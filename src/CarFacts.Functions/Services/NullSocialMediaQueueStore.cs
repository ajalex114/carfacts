using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Services;

/// <summary>
/// No-op social media queue store used when Cosmos DB is not configured.
/// </summary>
public sealed class NullSocialMediaQueueStore : ISocialMediaQueueStore
{
    private readonly ILogger<NullSocialMediaQueueStore> _logger;

    public NullSocialMediaQueueStore(ILogger<NullSocialMediaQueueStore> logger) => _logger = logger;

    public Task AddItemsAsync(IEnumerable<SocialMediaQueueItem> items, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Cosmos DB not configured — skipping social media queue storage");
        return Task.CompletedTask;
    }

    public Task<SocialMediaQueueItem?> GetRandomItemAsync(string platform, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Cosmos DB not configured — no queue items available");
        return Task.FromResult<SocialMediaQueueItem?>(null);
    }

    public Task DeleteItemAsync(string id, string platform, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
