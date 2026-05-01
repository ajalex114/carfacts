using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Regenerates the RSS feed XML in Blob Storage after a new post is published.
/// Best-effort: failures are logged but do not fail the orchestration.
/// </summary>
public sealed class GenerateRssFeedActivity
{
    private readonly IRssFeedService _rssFeedService;
    private readonly ILogger<GenerateRssFeedActivity> _logger;

    public GenerateRssFeedActivity(
        IRssFeedService rssFeedService,
        ILogger<GenerateRssFeedActivity> logger)
    {
        _rssFeedService = rssFeedService;
        _logger = logger;
    }

    [Function(nameof(GenerateRssFeedActivity))]
    public async Task<bool> Run([ActivityTrigger] string trigger)
    {
        _logger.LogInformation("Regenerating RSS feed (trigger={Trigger})", trigger);
        await _rssFeedService.RegenerateFeedAsync();
        _logger.LogInformation("RSS feed regenerated successfully");
        return true;
    }
}
