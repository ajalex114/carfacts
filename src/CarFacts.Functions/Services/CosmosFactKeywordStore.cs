using CarFacts.Functions.Configuration;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Services;

public sealed class CosmosFactKeywordStore : IFactKeywordStore
{
    private readonly Container _container;
    private readonly ILogger<CosmosFactKeywordStore> _logger;

    public CosmosFactKeywordStore(
        CosmosClient cosmosClient,
        IOptions<CosmosDbSettings> settings,
        ILogger<CosmosFactKeywordStore> logger)
    {
        _container = cosmosClient.GetContainer(settings.Value.DatabaseName, settings.Value.ContainerName);
        _logger = logger;
    }

    public async Task UpsertFactsAsync(IEnumerable<FactKeywordRecord> records, CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            await _container.UpsertItemAsync(record, new PartitionKey(record.Id), cancellationToken: cancellationToken);
            _logger.LogInformation("Stored keywords for fact: {Id} ({Count} keywords)", record.Id, record.Keywords.Count);
        }
    }
}
