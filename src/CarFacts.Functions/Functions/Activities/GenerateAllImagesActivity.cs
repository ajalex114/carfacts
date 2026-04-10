using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Generates all images sequentially, respecting API rate limits.
/// The underlying services (StabilityAI, TogetherAI) have built-in
/// delays between requests to avoid 429s.
/// </summary>
public sealed class GenerateAllImagesActivity
{
    private readonly IImageGenerationService _imageService;
    private readonly ILogger<GenerateAllImagesActivity> _logger;

    public GenerateAllImagesActivity(
        IImageGenerationService imageService,
        ILogger<GenerateAllImagesActivity> logger)
    {
        _imageService = imageService;
        _logger = logger;
    }

    [Function(nameof(GenerateAllImagesActivity))]
    public async Task<List<GeneratedImage>> Run(
        [ActivityTrigger] List<CarFact> facts)
    {
        _logger.LogInformation("Generating {Count} images (sequential with rate-limit handling)", facts.Count);

        try
        {
            var images = await _imageService.GenerateImagesAsync(facts);
            _logger.LogInformation("Generated {Count} images successfully", images.Count);
            return images;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Image generation failed: {Message}", ex.Message);
            return [];
        }
    }
}
