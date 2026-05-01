using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Regenerates all XML sitemaps in Blob Storage after a new post is published.
/// Best-effort: failures are logged but do not fail the orchestration.
/// </summary>
public sealed class GenerateSitemapActivity
{
    private readonly ISitemapService _sitemapService;
    private readonly ILogger<GenerateSitemapActivity> _logger;

    public GenerateSitemapActivity(
        ISitemapService sitemapService,
        ILogger<GenerateSitemapActivity> logger)
    {
        _sitemapService = sitemapService;
        _logger = logger;
    }

    [Function(nameof(GenerateSitemapActivity))]
    public async Task<bool> Run([ActivityTrigger] string trigger)
    {
        _logger.LogInformation("Regenerating sitemaps (trigger={Trigger})", trigger);
        await _sitemapService.RegenerateAllSitemapsAsync();
        _logger.LogInformation("Sitemaps regenerated successfully");
        return true;
    }
}
