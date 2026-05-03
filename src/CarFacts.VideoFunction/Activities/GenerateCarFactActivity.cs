using CarFacts.VideoFunction.Models;
using CarFacts.VideoFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Activities;

/// <summary>
/// Activity 0 — Selects a brand/model using a 3-level freshness strategy, then generates
/// a car fact via LLM.
///
/// Level 1 — Brand fresh: pick a brand not published in the last 5 days.
/// Level 2 — Model fresh: all brands used recently; pick a brand+model combo whose model
///            hasn't appeared in the last 5 days (queried from historical entries).
/// Level 3 — LRU fallback: every model was used recently; pick the oldest (least-recently-used)
///            brand+model from the tracking store.
///
/// After brand selection, attempts to find recent news (≤7 days) for the brand via NewsService.
/// If news is found it is passed as context to the LLM for a more topical fact.
/// </summary>
public class GenerateCarFactActivity(
    CarFactGenerationService factService,
    VideoTrackingService trackingService,
    NewsService newsService,
    ILogger<GenerateCarFactActivity> logger)
{
    [Function(nameof(GenerateCarFactActivity))]
    public async Task<GenerateCarFactActivityResult> Run(
        [ActivityTrigger] GenerateCarFactActivityInput input,
        FunctionContext ctx)
    {
        var selection = await SelectBrandModelAsync(input.JobId);

        logger.LogInformation("[{JobId}] GenerateCarFact: {Reason} → brand={Brand} model={Model} style={Style} (target {Min}-{Max}s)",
            input.JobId, selection.Reason, selection.Brand, selection.Model ?? "(any)",
            input.NarrationStyle, input.VideoLengthSecMin, input.VideoLengthSecMax);

        // Step B: fetch recent news for the resolved brand (non-fatal, falls back to null)
        var newsContext = await newsService.GetLatestNewsAsync(selection.Brand);
        if (newsContext != null)
            logger.LogInformation("[{JobId}] Using news context from {Source}: \"{Title}\"",
                input.JobId, newsContext.Source, newsContext.Title);

        var fact = await factService.GenerateFactAsync(
            selection, input.VideoLengthSecMin, input.VideoLengthSecMax, input.NarrationStyle, newsContext);

        logger.LogInformation("[{JobId}] GenerateCarFact: → \"{Preview}\"",
            input.JobId, fact[..Math.Min(80, fact.Length)]);
        return new GenerateCarFactActivityResult(fact);
    }

    private async Task<BrandModelSelection> SelectBrandModelAsync(string jobId)
    {
        // ── Level 1: find a brand not published in the last 5 days ──────────────
        var recentPairs  = await trackingService.GetRecentBrandModelsAsync(5);
        var recentBrands = recentPairs
            .Select(p => p.Brand.ToLowerInvariant())
            .ToHashSet();

        var availableBrands = CarFactGenerationService.AllBrands
            .Where(b => !recentBrands.Contains(b.ToLowerInvariant()))
            .ToArray();

        if (availableBrands.Length > 0)
        {
            var brand = availableBrands[Random.Shared.Next(availableBrands.Length)];
            logger.LogInformation("[{JobId}] Level1 brand pool: {Count} available, picked: {Brand}", jobId, availableBrands.Length, brand);
            return new BrandModelSelection(brand, null, "Level1-FreshBrand");
        }

        logger.LogInformation("[{JobId}] All {Count} brands used in last 5 days — falling back to Level2 (model freshness)", jobId, recentBrands.Count);

        // ── Level 2: find a brand+model combo whose model hasn't been used in 5 days ──
        var recentModels = recentPairs
            .Select(p => p.Model.ToLowerInvariant())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .ToHashSet();

        var allKnown = await trackingService.GetAllKnownBrandModelsAsync(60);
        var freshModelCandidates = allKnown
            .Where(p => !string.IsNullOrWhiteSpace(p.Model) &&
                        !recentModels.Contains(p.Model.ToLowerInvariant()))
            .ToArray();

        if (freshModelCandidates.Length > 0)
        {
            var pick = freshModelCandidates[Random.Shared.Next(freshModelCandidates.Length)];
            logger.LogInformation("[{JobId}] Level2 model pool: {Count} fresh models, picked: {Brand} {Model}", jobId, freshModelCandidates.Length, pick.Brand, pick.Model);
            return new BrandModelSelection(pick.Brand, pick.Model, "Level2-FreshModel");
        }

        logger.LogInformation("[{JobId}] All known models used in last 5 days — falling back to Level3 (LRU oldest)", jobId);

        // ── Level 3: pick the oldest (least-recently-used) brand+model ───────────
        var oldest = await trackingService.GetOldestEntryBrandModelAsync();
        if (oldest.HasValue)
        {
            logger.LogInformation("[{JobId}] Level3 LRU: {Brand} {Model}", jobId, oldest.Value.Brand, oldest.Value.Model);
            return new BrandModelSelection(oldest.Value.Brand, oldest.Value.Model, "Level3-OldestLRU");
        }

        // Absolute fallback (empty DB) — pick any brand at random
        var fallbackBrand = CarFactGenerationService.AllBrands[Random.Shared.Next(CarFactGenerationService.AllBrands.Length)];
        logger.LogInformation("[{JobId}] Level3 empty-DB fallback: {Brand}", jobId, fallbackBrand);
        return new BrandModelSelection(fallbackBrand, null, "Level3-EmptyDB");
    }
}
