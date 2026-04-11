using CarFacts.Functions.Models;

namespace CarFacts.Functions.Services.Interfaces;

public interface ISocialMediaQueueStore
{
    Task AddItemsAsync(IEnumerable<SocialMediaQueueItem> items, CancellationToken cancellationToken = default);

    Task<SocialMediaQueueItem?> GetRandomItemAsync(string platform, CancellationToken cancellationToken = default);

    Task DeleteItemAsync(string id, string platform, CancellationToken cancellationToken = default);
}
