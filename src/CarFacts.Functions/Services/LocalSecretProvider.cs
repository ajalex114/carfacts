using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Services;

public sealed class LocalSecretProvider : ISecretProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LocalSecretProvider> _logger;

    public LocalSecretProvider(IConfiguration configuration, ILogger<LocalSecretProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        var configKey = $"Secrets:{secretName}";
        var value = _configuration[configKey];

        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException($"Secret '{secretName}' not found in configuration under 'Secrets:{secretName}'.");

        _logger.LogWarning("Using local secret for {SecretName} — do NOT use in production", secretName);
        return Task.FromResult(value);
    }
}
