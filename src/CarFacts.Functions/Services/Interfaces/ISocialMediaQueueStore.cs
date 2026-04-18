using CarFacts.Functions.Models;

namespace CarFacts.Functions.Services.Interfaces;

public interface ISocialMediaQueueStore
{
    Task AddItemsAsync(IEnumerable<SocialMediaQueueItem> items, CancellationToken cancellationToken = default);

    Task<SocialMediaQueueItem?> GetRandomItemAsync(string platform, CancellationToken cancellationToken = default);

    Task DeleteItemAsync(string id, string platform, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all queued items that have a ScheduledAtUtc value, ordered by scheduled time ascending.
    /// </summary>
    Task<List<SocialMediaQueueItem>> GetPendingScheduledItemsAsync(CancellationToken cancellationToken = default);
}
