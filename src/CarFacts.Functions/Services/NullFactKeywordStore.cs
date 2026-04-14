using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Services;

/// <summary>
/// No-op keyword store used when Cosmos DB is not configured.
/// </summary>
public sealed class NullFactKeywordStore : IFactKeywordStore
{
    private readonly ILogger<NullFactKeywordStore> _logger;

    public NullFactKeywordStore(ILogger<NullFactKeywordStore> logger) => _logger = logger;

    public Task UpsertFactsAsync(IEnumerable<FactKeywordRecord> records, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Cosmos DB not configured — skipping keyword storage ({Count} records)", records.Count());
        return Task.CompletedTask;
    }

    public Task<List<FactKeywordRecord>> FindRelatedFactsAsync(
        List<string> keywords, string excludePostUrl, int maxResults = 5, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Cosmos DB not configured — skipping backlink lookup");
        return Task.FromResult(new List<FactKeywordRecord>());
    }

    public Task<List<FactKeywordRecord>> FindRelatedPostCandidatesAsync(
        List<string> allKeywords, string excludePostUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Cosmos DB not configured — skipping related post lookup");
        return Task.FromResult(new List<FactKeywordRecord>());
    }

    public Task IncrementBacklinkCountsAsync(IEnumerable<string> recordIds, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Cosmos DB not configured — skipping backlink count increment");
        return Task.CompletedTask;
    }

    public Task<List<FactKeywordRecord>> GetAllPostRecordsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Cosmos DB not configured — skipping social media post lookup");
        return Task.FromResult(new List<FactKeywordRecord>());
    }

    public Task IncrementSocialCountsAsync(string postUrl, string platform, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Cosmos DB not configured — skipping social count increment");
        return Task.CompletedTask;
    }

    public Task<List<FactKeywordRecord>> GetFactsForPinterestAsync(int maxResults = 20, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Cosmos DB not configured — skipping Pinterest fact lookup");
        return Task.FromResult(new List<FactKeywordRecord>());
    }

    public Task IncrementPinterestCountAsync(string recordId, string boardName, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Cosmos DB not configured — skipping Pinterest count increment");
        return Task.CompletedTask;
    }
}
