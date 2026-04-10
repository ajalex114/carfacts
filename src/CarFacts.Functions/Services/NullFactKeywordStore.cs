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
}
