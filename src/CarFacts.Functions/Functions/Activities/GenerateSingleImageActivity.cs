using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Generates a single image for one car fact.
/// Uses the fallback chain (StabilityAI → TogetherAI).
/// Returns null if all providers fail.
/// </summary>
public sealed class GenerateSingleImageActivity
{
    private readonly IImageGenerationService _imageService;
    private readonly ILogger<GenerateSingleImageActivity> _logger;

    public GenerateSingleImageActivity(
        IImageGenerationService imageService,
        ILogger<GenerateSingleImageActivity> logger)
    {
        _imageService = imageService;
        _logger = logger;
    }

    [Function(nameof(GenerateSingleImageActivity))]
    public async Task<GeneratedImage?> Run(
        [ActivityTrigger] CarFact fact)
    {
        _logger.LogInformation("Generating image for fact: {Model} ({Year})", fact.CarModel, fact.Year);

        try
        {
            var images = await _imageService.GenerateImagesAsync([fact]);
            if (images.Count > 0)
            {
                _logger.LogInformation("Image generated for {Model}", fact.CarModel);
                return images[0];
            }

            _logger.LogWarning("No image generated for {Model}", fact.CarModel);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Image generation failed for {Model}: {Message}", fact.CarModel, ex.Message);
            return null;
        }
    }
}
