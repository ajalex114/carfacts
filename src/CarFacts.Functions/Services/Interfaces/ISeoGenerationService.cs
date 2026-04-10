using CarFacts.Functions.Models;

namespace CarFacts.Functions.Services.Interfaces;

public interface ISeoGenerationService
{
    Task<SeoMetadata> GenerateSeoAsync(RawCarFactsContent content, CancellationToken cancellationToken = default);
}
