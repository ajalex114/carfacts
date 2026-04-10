using CarFacts.Functions.Models;

namespace CarFacts.Functions.Services.Interfaces;

public interface IFactKeywordStore
{
    Task UpsertFactsAsync(IEnumerable<FactKeywordRecord> records, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds related facts from Cosmos DB that share keywords with the given set.
    /// Excludes facts from the specified post URL. Prefers facts with lower backlinkCount.
    /// </summary>
    Task<List<FactKeywordRecord>> FindRelatedFactsAsync(
        List<string> keywords,
        string excludePostUrl,
        int maxResults = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds related posts (distinct by postUrl) that share keywords with any of the given keywords.
    /// Returns records grouped by post, preferring posts with lower total backlinkCount.
    /// Excludes the current post URL.
    /// </summary>
    Task<List<FactKeywordRecord>> FindRelatedPostCandidatesAsync(
        List<string> allKeywords,
        string excludePostUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments the backlinkCount for each specified record ID.
    /// </summary>
    Task IncrementBacklinkCountsAsync(IEnumerable<string> recordIds, CancellationToken cancellationToken = default);
}
