using CarFacts.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Stub activity that logs the scheduled post execution.
/// Will be replaced with actual Twitter/social posting logic later.
/// </summary>
public sealed class ExecuteScheduledPostActivity
{
    private readonly ILogger<ExecuteScheduledPostActivity> _logger;

    public ExecuteScheduledPostActivity(ILogger<ExecuteScheduledPostActivity> logger)
    {
        _logger = logger;
    }

    [Function(nameof(ExecuteScheduledPostActivity))]
    public Task<bool> Run(
        [ActivityTrigger] ScheduledPostInput input)
    {
        _logger.LogInformation(
            "Scheduled post executed at {Time} for {Platform} [{Type}]: {ItemId} — Content: {Content}",
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
            input.Platform,
            input.Type,
            input.ItemId,
            input.Content.Length > 80 ? input.Content[..80] + "..." : input.Content);

        return Task.FromResult(true);
    }
}
