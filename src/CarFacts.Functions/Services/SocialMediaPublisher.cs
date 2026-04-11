using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Services;

/// <summary>
/// Fans out social media posts to all enabled platforms.
/// Individual platform failures are logged but do not block other platforms.
/// </summary>
public sealed class SocialMediaPublisher
{
    private readonly IEnumerable<ISocialMediaService> _services;
    private readonly ILogger<SocialMediaPublisher> _logger;

    public SocialMediaPublisher(
        IEnumerable<ISocialMediaService> services,
        ILogger<SocialMediaPublisher> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task PublishAsync(string teaser, string postUrl, string postTitle, List<string> keywords, CancellationToken cancellationToken = default)
    {
        var enabledServices = _services.Where(s => s.IsEnabled).ToList();

        if (enabledServices.Count == 0)
        {
            _logger.LogInformation("Social media publishing skipped — no platforms enabled");
            return;
        }

        _logger.LogInformation("Publishing to {Count} social platform(s): {Platforms}",
            enabledServices.Count,
            string.Join(", ", enabledServices.Select(s => s.PlatformName)));

        var tasks = enabledServices.Select(async service =>
        {
            try
            {
                await service.PostAsync(teaser, postUrl, postTitle, keywords, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post to {Platform} — continuing with other platforms", service.PlatformName);
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation("Social media publishing complete");
    }

    /// <summary>
    /// Posts pre-formatted content to a specific platform by name.
    /// </summary>
    public async Task PublishRawAsync(string platformName, string content, CancellationToken cancellationToken = default)
    {
        var service = _services.FirstOrDefault(s =>
            s.IsEnabled && s.PlatformName.Equals(platformName, StringComparison.OrdinalIgnoreCase));

        if (service == null)
        {
            _logger.LogWarning("Platform {Platform} not found or not enabled — skipping raw post", platformName);
            return;
        }

        await service.PostRawAsync(content, cancellationToken);
        _logger.LogInformation("Raw post published to {Platform}", platformName);
    }

    /// <summary>
    /// Returns the names of all enabled social media platforms.
    /// </summary>
    public List<string> GetEnabledPlatformNames()
    {
        return _services.Where(s => s.IsEnabled).Select(s => s.PlatformName).ToList();
    }
}
