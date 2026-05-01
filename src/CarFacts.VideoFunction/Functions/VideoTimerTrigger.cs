using CarFacts.VideoFunction.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Functions;

/// <summary>
/// Fires at 10:00, 11:00, 12:00, 13:00, and 14:00 IST every day (5 videos/day).
/// Each run starts a VideoOrchestrator with no fact — the orchestrator's Step 0
/// calls GenerateCarFactActivity to generate a fresh LLM fact independently.
///
/// Timezone: set WEBSITE_TIME_ZONE = "India Standard Time" in Azure app settings.
/// NCRONTAB: "0 0 10-14 * * *" fires at minute 0, hours 10–14, every day.
/// </summary>
public class VideoTimerTrigger(
    IConfiguration configuration,
    ILogger<VideoTimerTrigger> logger)
{
    [Function(nameof(VideoTimerTrigger))]
    public async Task Run(
        [TimerTrigger("0 0 10-14 * * *")] TimerInfo timer,
        [DurableClient] DurableTaskClient client,
        FunctionContext ctx)
    {
        logger.LogInformation("VideoTimerTrigger fired at {Time} UTC", DateTime.UtcNow);

        var storageConn = configuration["Storage:ConnectionString"]
            ?? throw new InvalidOperationException("Storage:ConnectionString not configured");

        // No fact provided — orchestrator's Step 0 will generate one via LLM
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(VideoOrchestrator),
            new OrchestratorInput(
                JobId:                 Guid.NewGuid().ToString("N")[..16],
                Fact:                  null,
                StorageConnectionString: storageConn,
                ImageSearchQuery:      null));

        logger.LogInformation("VideoTimerTrigger: started orchestration {InstanceId}", instanceId);
    }
}
