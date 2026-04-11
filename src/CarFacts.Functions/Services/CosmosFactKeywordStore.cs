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

    public async Task<List<FactKeywordRecord>> FindRelatedFactsAsync(
        List<string> keywords,
        string excludePostUrl,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        if (keywords.Count == 0)
            return [];

        // Build ARRAY_CONTAINS OR chain for keyword matching
        var conditions = keywords
            .Select((kw, i) => $"ARRAY_CONTAINS(c.keywords, @kw{i})")
            .ToList();
        var whereClause = string.Join(" OR ", conditions);

        var query = $"SELECT * FROM c WHERE ({whereClause}) AND c.postUrl != @excludeUrl";

        var queryDef = new QueryDefinition(query);
        for (int i = 0; i < keywords.Count; i++)
            queryDef = queryDef.WithParameter($"@kw{i}", keywords[i]);
        queryDef = queryDef.WithParameter("@excludeUrl", excludePostUrl);

        var results = new List<FactKeywordRecord>();
        using var iterator = _container.GetItemQueryIterator<FactKeywordRecord>(queryDef);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        _logger.LogInformation("Found {Count} related facts for keywords [{Keywords}]",
            results.Count, string.Join(", ", keywords.Take(5)));

        return results;
    }

    public async Task<List<FactKeywordRecord>> FindRelatedPostCandidatesAsync(
        List<string> allKeywords,
        string excludePostUrl,
        CancellationToken cancellationToken = default)
    {
        if (allKeywords.Count == 0)
            return [];

        // Take top 15 keywords to keep query manageable
        var topKeywords = allKeywords.Take(15).ToList();
        var conditions = topKeywords
            .Select((kw, i) => $"ARRAY_CONTAINS(c.keywords, @kw{i})")
            .ToList();
        var whereClause = string.Join(" OR ", conditions);

        var query = $"SELECT * FROM c WHERE ({whereClause}) AND c.postUrl != @excludeUrl";

        var queryDef = new QueryDefinition(query);
        for (int i = 0; i < topKeywords.Count; i++)
            queryDef = queryDef.WithParameter($"@kw{i}", topKeywords[i]);
        queryDef = queryDef.WithParameter("@excludeUrl", excludePostUrl);

        var results = new List<FactKeywordRecord>();
        using var iterator = _container.GetItemQueryIterator<FactKeywordRecord>(queryDef);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        _logger.LogInformation("Found {Count} related post candidates from {Keywords} keywords",
            results.Count, topKeywords.Count);

        return results;
    }

    public async Task IncrementBacklinkCountsAsync(IEnumerable<string> recordIds, CancellationToken cancellationToken = default)
    {
        foreach (var id in recordIds)
        {
            try
            {
                var response = await _container.ReadItemAsync<FactKeywordRecord>(id, new PartitionKey(id), cancellationToken: cancellationToken);
                var record = response.Resource;
                record.BacklinkCount++;
                await _container.ReplaceItemAsync(record, id, new PartitionKey(id), cancellationToken: cancellationToken);
                _logger.LogInformation("Incremented backlink count for {Id} to {Count}", id, record.BacklinkCount);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Record {Id} not found for backlink increment — skipping", id);
            }
        }
    }

    public async Task<List<FactKeywordRecord>> GetAllPostRecordsAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c");
        var results = new List<FactKeywordRecord>();
        using var iterator = _container.GetItemQueryIterator<FactKeywordRecord>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        _logger.LogInformation("Retrieved {Count} total records for social media selection", results.Count);
        return results;
    }

    public async Task IncrementSocialCountsAsync(string postUrl, string platform, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.postUrl = @postUrl")
            .WithParameter("@postUrl", postUrl);

        var records = new List<FactKeywordRecord>();
        using var iterator = _container.GetItemQueryIterator<FactKeywordRecord>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            records.AddRange(response);
        }

        foreach (var record in records)
        {
            switch (platform.ToLowerInvariant())
            {
                case "twitter":
                    record.TwitterCount++;
                    break;
            }
            record.BacklinkCount++;

            await _container.ReplaceItemAsync(record, record.Id, new PartitionKey(record.Id), cancellationToken: cancellationToken);
            _logger.LogInformation("Incremented {Platform} count for {Id} (twitter={Tc}, backlink={Bc})",
                platform, record.Id, record.TwitterCount, record.BacklinkCount);
        }
    }
}
