using CarFacts.VideoFunction.Models;
using CarFacts.VideoFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Activities;

/// <summary>
/// Activity 1.5 — Extracts a clean image search query from the car fact text.
/// Uses Azure OpenAI if configured; falls back to regex brand/model/year extraction.
/// Runs before PlanSegmentsActivity so all segments share one consistent query.
/// </summary>
public class GenerateSearchQueryActivity(
    ImageQueryExtractorService queryExtractor,
    ILogger<GenerateSearchQueryActivity> logger)
{
    [Function(nameof(GenerateSearchQueryActivity))]
    public async Task<GenerateQueryActivityResult> Run(
        [ActivityTrigger] GenerateQueryActivityInput input,
        FunctionContext ctx)
    {
        logger.LogInformation("[{JobId}] GenerateSearchQuery: extracting from fact text", input.JobId);
        var query = await queryExtractor.ExtractQueryAsync(input.Fact);
        logger.LogInformation("[{JobId}] GenerateSearchQuery: → \"{Query}\"", input.JobId, query);
        return new GenerateQueryActivityResult(query);
    }
}
