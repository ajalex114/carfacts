using CarFacts.Functions.Functions.Activities;
using CarFacts.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions;

/// <summary>
/// Timer trigger that fires 6 times per day (every 4 hours).
/// Each invocation posts one item from the social media queue per enabled platform.
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
        _logger.LogInformation("Social media posting trigger fired at {Time}", DateTime.UtcNow);

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(SocialMediaPostingOrchestrator));

        _logger.LogInformation("Started posting orchestration: {InstanceId}", instanceId);
    }
}
