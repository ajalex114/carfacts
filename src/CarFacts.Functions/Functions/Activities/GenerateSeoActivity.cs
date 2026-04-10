using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

public sealed class GenerateSeoActivity
{
    private readonly ISeoGenerationService _seoService;
    private readonly ILogger<GenerateSeoActivity> _logger;

    public GenerateSeoActivity(
        ISeoGenerationService seoService,
        ILogger<GenerateSeoActivity> logger)
    {
        _seoService = seoService;
        _logger = logger;
    }

    [Function(nameof(GenerateSeoActivity))]
    public async Task<SeoMetadata> Run(
        [ActivityTrigger] RawCarFactsContent content)
    {
        _logger.LogInformation("Generating SEO metadata for {Count} facts", content.Facts.Count);
        var seo = await _seoService.GenerateSeoAsync(content);
        _logger.LogInformation("SEO generated: {Title}", seo.MainTitle);
        return seo;
    }
}
