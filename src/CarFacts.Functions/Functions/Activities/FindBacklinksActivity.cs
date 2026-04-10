using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

public sealed class FindBacklinksActivity
{
    private readonly IFactKeywordStore _store;
    private readonly ILogger<FindBacklinksActivity> _logger;
    private static readonly Random Rng = new();

    public FindBacklinksActivity(IFactKeywordStore store, ILogger<FindBacklinksActivity> logger)
    {
        _store = store;
        _logger = logger;
    }

    [Function(nameof(FindBacklinksActivity))]
    public async Task<List<BacklinkSuggestion>> Run([ActivityTrigger] FindBacklinksInput input)
    {
        _logger.LogInformation("Finding backlinks for {Count} facts", input.FactKeywords.Count);

        var suggestions = new List<BacklinkSuggestion>();
        var usedRecordIds = new HashSet<string>();

        foreach (var factKw in input.FactKeywords)
        {
            if (factKw.Keywords.Count == 0)
            {
                _logger.LogWarning("Fact {Index} has no keywords — skipping backlink lookup", factKw.FactIndex);
                continue;
            }

            var candidates = await _store.FindRelatedFactsAsync(
                factKw.Keywords,
                input.CurrentPostUrl,
                maxResults: 20);

            // Remove already-used records (don't link to the same fact twice in one post)
            candidates = candidates.Where(c => !usedRecordIds.Contains(c.Id)).ToList();

            if (candidates.Count == 0)
            {
                _logger.LogInformation("No backlink candidates for fact {Index}", factKw.FactIndex);
                continue;
            }

            // Weighted random selection: prefer lower backlinkCount
            var selected = SelectWeightedRandom(candidates);
            usedRecordIds.Add(selected.Id);

            suggestions.Add(new BacklinkSuggestion
            {
                FactIndex = factKw.FactIndex,
                TargetFactUrl = selected.FactUrl,
                TargetTitle = selected.Title,
                TargetCarModel = selected.CarModel,
                TargetYear = selected.Year,
                TargetRecordId = selected.Id
            });

            _logger.LogInformation("Fact {Index} → backlink to {CarModel} ({Year}): {Url}",
                factKw.FactIndex, selected.CarModel, selected.Year, selected.FactUrl);
        }

        _logger.LogInformation("Generated {Count} backlink suggestions", suggestions.Count);
        return suggestions;
    }

    /// <summary>
    /// Weighted random selection favoring records with lower backlinkCount.
    /// Weight = 1 / (backlinkCount + 1), so a record with 0 backlinks has weight 1,
    /// one with 5 backlinks has weight ~0.17.
    /// </summary>
    private static FactKeywordRecord SelectWeightedRandom(List<FactKeywordRecord> candidates)
    {
        var weights = candidates.Select(c => 1.0 / (c.BacklinkCount + 1)).ToList();
        var totalWeight = weights.Sum();

        var roll = Rng.NextDouble() * totalWeight;
        var cumulative = 0.0;

        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
                return candidates[i];
        }

        return candidates[^1];
    }
}
