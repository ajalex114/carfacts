using CarFacts.Functions.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Returns whether Web Stories creation is enabled.
/// </summary>
public sealed class GetWebStoriesEnabledActivity
{
    private readonly WebStoriesSettings _settings;
    private readonly ILogger<GetWebStoriesEnabledActivity> _logger;

    public GetWebStoriesEnabledActivity(
        IOptions<WebStoriesSettings> settings,
        ILogger<GetWebStoriesEnabledActivity> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    [Function(nameof(GetWebStoriesEnabledActivity))]
    public bool Run([ActivityTrigger] string input)
    {
        _logger.LogInformation("Web Stories enabled: {Enabled}", _settings.Enabled);
        return _settings.Enabled;
    }
}
