using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CarFacts.Functions.Configuration;

namespace CarFacts.Functions.Functions;

/// <summary>
/// Timer trigger for Pinterest posting. Posts one pin per invocation.
/// Default schedule: 6 times per day, favoring US evening hours.
/// </summary>
public sealed class PinterestPostingTrigger
{
    private readonly ILogger<PinterestPostingTrigger> _logger;
    private readonly SocialMediaSettings _settings;

    public PinterestPostingTrigger(
        ILogger<PinterestPostingTrigger> logger,
        IOptions<SocialMediaSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    [Function(nameof(PinterestPostingTrigger))]
    public async Task Run(
        [TimerTrigger("%SocialMedia:PinterestPostingCronExpression%")] TimerInfo timer,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        if (!_settings.PinterestEnabled)
        {
            _logger.LogInformation("Pinterest posting disabled — skipping");
            return;
        }

        _logger.LogInformation("Pinterest posting trigger fired at {Time}", DateTime.UtcNow);

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(PinterestPostingOrchestrator));

        _logger.LogInformation("Started Pinterest posting orchestration: {InstanceId}", instanceId);
    }
}
