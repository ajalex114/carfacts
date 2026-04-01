using CarFacts.Functions.Models;

namespace CarFacts.Functions.Services.Interfaces;

public interface IImageGenerationService
{
    Task<List<GeneratedImage>> GenerateImagesAsync(List<CarFact> facts, CancellationToken cancellationToken = default);
}
