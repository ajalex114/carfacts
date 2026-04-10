using CarFacts.Functions.Models;

namespace CarFacts.Functions.Services.Interfaces;

public interface IContentGenerationService
{
    Task<RawCarFactsContent> GenerateFactsAsync(string todayDate, CancellationToken cancellationToken = default);
}
