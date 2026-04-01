using CarFacts.Functions.Models;

namespace CarFacts.Functions.Services.Interfaces;

public interface IContentGenerationService
{
    Task<CarFactsResponse> GenerateFactsAsync(string todayDate, CancellationToken cancellationToken = default);
}
