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
    public async Task<BacklinksResult> Run([ActivityTrigger] FindBacklinksInput input)
    {
        _logger.LogInformation("Finding backlinks for {Count} facts", input.FactKeywords.Count);

        var result = new BacklinksResult();
        var usedRecordIds = new HashSet<string>();
        var usedPostUrls = new HashSet<string>();

        // --- Per-fact inline backlinks ---
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
            usedPostUrls.Add(selected.PostUrl);

            result.Backlinks.Add(new BacklinkSuggestion
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

        // --- Bottom section: 4 related posts (distinct by postUrl) ---
        var allKeywords = input.FactKeywords.SelectMany(fk => fk.Keywords).Distinct().ToList();
        var postCandidates = await _store.FindRelatedPostCandidatesAsync(allKeywords, input.CurrentPostUrl);

        // Group by postUrl, pick one representative record per post
        var postGroups = postCandidates
            .Where(c => !usedPostUrls.Contains(c.PostUrl)) // Exclude posts already used in inline backlinks
            .GroupBy(c => c.PostUrl)
            .Select(g =>
            {
                var records = g.ToList();
                // Total backlink count for this post (lower = preferred)
                var totalBacklinks = records.Sum(r => r.BacklinkCount);
                // Pick a record that has an imageUrl if possible
                var best = records.FirstOrDefault(r => !string.IsNullOrEmpty(r.ImageUrl)) ?? records.First();
                return new { PostUrl = g.Key, Record = best, TotalBacklinks = totalBacklinks, AllRecordIds = records.Select(r => r.Id).ToList() };
            })
            .ToList();

        // Weighted random selection for 4 posts
        var selectedPosts = new List<RelatedPostSuggestion>();
        var remainingGroups = postGroups.ToList();

        for (int i = 0; i < 4 && remainingGroups.Count > 0; i++)
        {
            var weights = remainingGroups.Select(g => 1.0 / (g.TotalBacklinks + 1)).ToList();
            var totalWeight = weights.Sum();
            var roll = Rng.NextDouble() * totalWeight;
            var cumulative = 0.0;

            int selectedIdx = remainingGroups.Count - 1;
            for (int j = 0; j < remainingGroups.Count; j++)
            {
                cumulative += weights[j];
                if (roll <= cumulative) { selectedIdx = j; break; }
            }

            var picked = remainingGroups[selectedIdx];
            remainingGroups.RemoveAt(selectedIdx);

            selectedPosts.Add(new RelatedPostSuggestion
            {
                PostUrl = picked.PostUrl,
                PostTitle = !string.IsNullOrEmpty(picked.Record.PostTitle)
                    ? picked.Record.PostTitle
                    : picked.Record.Title, // Fallback to fact title if postTitle not set
                ImageUrl = picked.Record.ImageUrl,
                SourceRecordIds = picked.AllRecordIds
            });

            _logger.LogInformation("Related post {Index}: {PostUrl}", i + 1, picked.PostUrl);
        }

        result.RelatedPosts = selectedPosts;
        _logger.LogInformation("Generated {Backlinks} backlinks + {Related} related posts",
            result.Backlinks.Count, result.RelatedPosts.Count);
        return result;
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
