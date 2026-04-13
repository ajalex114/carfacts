using CarFacts.Functions.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions;

/// <summary>
/// Timer trigger that starts the Durable Functions orchestrator.
/// Replaces the old monolithic DailyCarFactsFunction.
/// </summary>
public sealed class CarFactsTimerTrigger
{
    private readonly ILogger<CarFactsTimerTrigger> _logger;

    public CarFactsTimerTrigger(ILogger<CarFactsTimerTrigger> logger)
    {
        _logger = logger;
    }

    [Function(nameof(CarFactsTimerTrigger))]
    public async Task Run(
        [TimerTrigger("%Schedule:CronExpression%")] TimerInfo timer,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("CarFacts timer triggered at {Time}", DateTime.UtcNow);

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(CarFactsOrchestrator));

        _logger.LogInformation("Started orchestration: {InstanceId}", instanceId);
    }
}
