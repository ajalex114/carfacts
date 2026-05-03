using CarFacts.VideoFunction.Models;
using CarFacts.VideoFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Activities;

/// <summary>
/// Activity 6 — Writes a published-video tracking entry to Cosmos DB.
/// Extracts brand and model from the fact text using the known brand list.
/// Non-fatal: if Cosmos is unavailable the pipeline result is unaffected.
/// </summary>
public class SavePublishedVideoActivity(
    VideoTrackingService trackingService,
    ILogger<SavePublishedVideoActivity> logger)
{
    [Function(nameof(SavePublishedVideoActivity))]
    public async Task<SavePublishedVideoActivityResult> Run(
        [ActivityTrigger] SavePublishedVideoActivityInput input,
        FunctionContext ctx)
    {
        var (brand, model) = ExtractBrandModel(input.Fact);
        var keywords = BuildKeywords(brand, model, input.Fact);

        var entry = new VideoTrackingEntry
        {
            Id             = input.JobId,
            JobId          = input.JobId,
            YouTubeVideoId = input.YouTubeVideoId,
            YouTubeVideoUrl= input.YouTubeVideoUrl,
            PublishedAt    = DateTimeOffset.UtcNow.ToString("O"),
            Brand          = brand,
            Model          = model,
            Fact           = input.Fact,
            Keywords       = keywords,
            BacklinkCount  = 0,
            RelatedVideoId = input.RelatedVideoId,
            Platform       = input.Platform
        };

        await trackingService.SavePublishedVideoAsync(entry);
        logger.LogInformation("[{JobId}] Tracking: brand={Brand} model={Model}", input.JobId, brand, model);

        // Increment the backlink counter on the related video (if any)
        if (!string.IsNullOrWhiteSpace(input.RelatedVideoId) &&
            !string.IsNullOrWhiteSpace(input.RelatedVideoBrand))
        {
            await trackingService.IncrementBacklinkCountAsync(input.RelatedVideoId, input.RelatedVideoBrand);
        }

        return new SavePublishedVideoActivityResult(true);
    }

    private static (string brand, string? model) ExtractBrandModel(string fact)
    {
        // Match against known brands, longest first to prefer "Ford Mustang Shelby GT500" over "Ford".
        var sorted = CarFactGenerationService.AllBrands
            .OrderByDescending(b => b.Length);

        foreach (var candidate in sorted)
        {
            if (fact.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            {
                // Extract model: words after the brand name that look like a proper noun or alphanumeric.
                var idx = fact.IndexOf(candidate, StringComparison.OrdinalIgnoreCase);
                var after = fact[(idx + candidate.Length)..].TrimStart('\'', '\u2019', 's', ' ');
                var words = after.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var modelWords = words
                    .Take(3)
                    .TakeWhile(w => w.Length > 0 && (char.IsUpper(w[0]) || char.IsDigit(w[0])))
                    .ToArray();
                var model = modelWords.Length > 0 ? string.Join(" ", modelWords) : null;
                return (candidate, model);
            }
        }
        return ("Unknown", null);
    }

    private static List<string> BuildKeywords(string brand, string? model, string fact)
    {
        var kw = new List<string> { brand };
        if (!string.IsNullOrWhiteSpace(model))
            kw.Add(model);

        // Extract year if present (4 digits starting with 19 or 20).
        var yearMatch = System.Text.RegularExpressions.Regex.Match(fact, @"\b(19|20)\d{2}\b");
        if (yearMatch.Success)
            kw.Add(yearMatch.Value);

        return kw.Distinct().ToList();
    }
}
