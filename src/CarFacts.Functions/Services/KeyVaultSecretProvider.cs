using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using CarFacts.Functions.Configuration;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Services;

public sealed class KeyVaultSecretProvider : ISecretProvider
{
    private readonly SecretClient _client;
    private readonly ILogger<KeyVaultSecretProvider> _logger;

    public KeyVaultSecretProvider(IOptions<KeyVaultSettings> settings, ILogger<KeyVaultSecretProvider> logger)
    {
        _logger = logger;
        var vaultUri = new Uri(settings.Value.VaultUri);
        _client = new SecretClient(vaultUri, new DefaultAzureCredential());
    }

    public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving secret {SecretName} from Key Vault", secretName);

        var response = await _client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
        return response.Value.Value;
    }
}
