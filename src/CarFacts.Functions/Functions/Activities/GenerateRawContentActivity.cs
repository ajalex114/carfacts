using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

public sealed class GenerateRawContentActivity
{
    private readonly IContentGenerationService _contentService;
    private readonly ILogger<GenerateRawContentActivity> _logger;

    public GenerateRawContentActivity(
        IContentGenerationService contentService,
        ILogger<GenerateRawContentActivity> logger)
    {
        _contentService = contentService;
        _logger = logger;
    }

    [Function(nameof(GenerateRawContentActivity))]
    public async Task<RawCarFactsContent> Run(
        [ActivityTrigger] string todayDate)
    {
        _logger.LogInformation("Generating raw car facts for {Date}", todayDate);
        var content = await _contentService.GenerateFactsAsync(todayDate);
        _logger.LogInformation("Generated {Count} facts", content.Facts.Count);
        return content;
    }
}
