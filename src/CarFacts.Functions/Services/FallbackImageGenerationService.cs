using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Services;

/// <summary>
/// Tries multiple image providers in order. Returns empty list if all fail,
/// allowing the pipeline to publish text-only.
/// </summary>
public sealed class FallbackImageGenerationService : IImageGenerationService
{
    private readonly IReadOnlyList<IImageGenerationService> _providers;
    private readonly ILogger<FallbackImageGenerationService> _logger;

    public FallbackImageGenerationService(
        IEnumerable<IImageGenerationService> providers,
        ILogger<FallbackImageGenerationService> logger)
    {
        _providers = providers.ToList();
        _logger = logger;
    }

    public async Task<List<GeneratedImage>> GenerateImagesAsync(
        List<CarFact> facts, CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < _providers.Count; i++)
        {
            var provider = _providers[i];
            var providerName = provider.GetType().Name;

            try
            {
                _logger.LogInformation("Trying image provider {Index}/{Total}: {Provider}",
                    i + 1, _providers.Count, providerName);

                var images = await provider.GenerateImagesAsync(facts, cancellationToken);

                if (images.Count > 0)
                {
                    _logger.LogInformation("Image generation succeeded with {Provider}", providerName);
                    return images;
                }

                _logger.LogWarning("{Provider} returned zero images, trying next provider", providerName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Image provider {Provider} failed: {Message}. Trying next provider",
                    providerName, ex.Message);
            }
        }

        _logger.LogWarning("All {Count} image providers failed — proceeding without images", _providers.Count);
        return [];
    }
}
