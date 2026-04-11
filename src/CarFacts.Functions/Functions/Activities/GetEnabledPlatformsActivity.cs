using CarFacts.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

public sealed class GetEnabledPlatformsActivity
{
    private readonly SocialMediaPublisher _publisher;
    private readonly ILogger<GetEnabledPlatformsActivity> _logger;

    public GetEnabledPlatformsActivity(
        SocialMediaPublisher publisher,
        ILogger<GetEnabledPlatformsActivity> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    [Function(nameof(GetEnabledPlatformsActivity))]
    public Task<List<string>> Run(
        [ActivityTrigger] string trigger)
    {
        var platforms = _publisher.GetEnabledPlatformNames();
        _logger.LogInformation("Enabled social media platforms: {Platforms}",
            platforms.Count > 0 ? string.Join(", ", platforms) : "none");
        return Task.FromResult(platforms);
    }
}
