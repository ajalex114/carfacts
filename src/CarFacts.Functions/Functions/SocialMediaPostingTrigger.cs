using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions;

/// <summary>
/// Timer trigger that fires once per day (using the posting cron expression).
/// Starts the ScheduledPostingOrchestrator which reads all pending items from Cosmos
/// and creates independent durable timer instances for each scheduled post.
/// </summary>
public sealed class SocialMediaPostingTrigger
{
    private readonly ILogger<SocialMediaPostingTrigger> _logger;

    public SocialMediaPostingTrigger(ILogger<SocialMediaPostingTrigger> logger)
    {
        _logger = logger;
    }

    [Function(nameof(SocialMediaPostingTrigger))]
    public async Task Run(
        [TimerTrigger("%SocialMedia:PostingCronExpression%")] TimerInfo timer,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Social media posting trigger fired at {Time} — starting scheduled posting orchestrator", DateTime.UtcNow);

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(ScheduledPostingOrchestrator),
            cancellationToken);

        _logger.LogInformation("Started ScheduledPostingOrchestrator: {InstanceId}", instanceId);
    }
}
