using CarFacts.Functions.Configuration;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Services;

public sealed class CosmosSocialMediaQueueStore : ISocialMediaQueueStore
{
    private readonly Container _container;
    private readonly ILogger<CosmosSocialMediaQueueStore> _logger;

    public CosmosSocialMediaQueueStore(
        CosmosClient cosmosClient,
        IOptions<CosmosDbSettings> settings,
        ILogger<CosmosSocialMediaQueueStore> logger)
    {
        _container = cosmosClient.GetContainer(settings.Value.DatabaseName, "social-media-queue");
        _logger = logger;
    }

    public async Task AddItemsAsync(IEnumerable<SocialMediaQueueItem> items, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            await _container.UpsertItemAsync(item, new PartitionKey(item.Platform), cancellationToken: cancellationToken);
            _logger.LogInformation("Queued {Type} item for {Platform}: {Id}", item.Type, item.Platform, item.Id);
        }
    }

    public async Task<SocialMediaQueueItem?> GetRandomItemAsync(string platform, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.platform = @platform")
            .WithParameter("@platform", platform);

        var items = new List<SocialMediaQueueItem>();
        using var iterator = _container.GetItemQueryIterator<SocialMediaQueueItem>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            items.AddRange(response);
        }

        if (items.Count == 0)
        {
            _logger.LogInformation("No queued items for {Platform}", platform);
            return null;
        }

        var selected = items[Random.Shared.Next(items.Count)];
        _logger.LogInformation("Selected {Type} item {Id} for {Platform} (from {Count} available)",
            selected.Type, selected.Id, platform, items.Count);
        return selected;
    }

    public async Task DeleteItemAsync(string id, string platform, CancellationToken cancellationToken = default)
    {
        try
        {
            await _container.DeleteItemAsync<SocialMediaQueueItem>(id, new PartitionKey(platform), cancellationToken: cancellationToken);
            _logger.LogInformation("Deleted queue item {Id} for {Platform}", id, platform);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Queue item {Id} already deleted for {Platform}", id, platform);
        }
    }
}
