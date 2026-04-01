using CarFacts.Functions.Models;

namespace CarFacts.Functions.Services.Interfaces;

public interface ISecretProvider
{
    Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
}
