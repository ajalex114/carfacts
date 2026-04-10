using CarFacts.Functions.Helpers;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

public sealed class StoreFactKeywordsActivity
{
    private readonly IFactKeywordStore _store;
    private readonly ILogger<StoreFactKeywordsActivity> _logger;

    public StoreFactKeywordsActivity(IFactKeywordStore store, ILogger<StoreFactKeywordsActivity> logger)
    {
        _store = store;
        _logger = logger;
    }

    [Function(nameof(StoreFactKeywordsActivity))]
    public async Task<bool> Run([ActivityTrigger] StoreFactKeywordsInput input)
    {
        _logger.LogInformation("Storing keywords for {Count} facts from {PostUrl}",
            input.Content.Facts.Count, input.PostUrl);

        var records = new List<FactKeywordRecord>();
        var datePrefix = input.PublishDate.ToString("yyyy-MM-dd");

        foreach (var (fact, index) in input.Content.Facts.Select((f, i) => (f, i)))
        {
            var anchorId = SlugHelper.GenerateAnchorId(fact);
            var factKeywords = input.Seo.FactKeywords
                .FirstOrDefault(fk => fk.FactIndex == index)?.Keywords ?? [];

            records.Add(new FactKeywordRecord
            {
                Id = $"{datePrefix}_{anchorId}",
                PostUrl = input.PostUrl,
                AnchorId = anchorId,
                FactUrl = $"{input.PostUrl}#{anchorId}",
                Title = fact.CatchyTitle,
                CarModel = fact.CarModel,
                Year = fact.Year,
                Keywords = factKeywords,
                BacklinkCount = 0,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _store.UpsertFactsAsync(records);
        _logger.LogInformation("Stored {Count} fact keyword records", records.Count);

        // Increment backlink counts for all facts that were linked to in this post
        if (input.Backlinks.Count > 0)
        {
            var linkedRecordIds = input.Backlinks.Select(b => b.TargetRecordId).ToList();
            _logger.LogInformation("Incrementing backlink counts for {Count} linked facts", linkedRecordIds.Count);
            await _store.IncrementBacklinkCountsAsync(linkedRecordIds);
        }

        return true;
    }
}
