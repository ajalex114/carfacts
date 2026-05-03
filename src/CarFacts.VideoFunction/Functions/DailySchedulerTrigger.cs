using CarFacts.VideoFunction.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Functions;

/// <summary>
/// Fires at 6:00 AM IST every day (WEBSITE_TIME_ZONE = "India Standard Time").
/// Reads per-platform config from app settings and starts the DailySchedulerOrchestrator,
/// which generates randomised publication times and fans out ScheduledVideoOrchestrator instances.
///
/// RunOnStartup = false → deploying never triggers an unexpected video run.
///
/// App settings expected (double-underscore = colon separator in .NET config):
///   Platforms__YouTube__Enabled          = true
///   Platforms__YouTube__VideosPerDay     = 20
///   Platforms__YouTube__VideoLengthSecMin = 15
///   Platforms__YouTube__VideoLengthSecMax = 18
///   (repeat for Facebook, Rumble, etc. as needed)
/// </summary>
public class DailySchedulerTrigger(
    IConfiguration configuration,
    ILogger<DailySchedulerTrigger> logger)
{
    // Known platforms — extend this list when adding Facebook Reels, Rumble, etc.
    private static readonly string[] KnownPlatforms = ["YouTube", "Facebook", "Rumble"];

    [Function(nameof(DailySchedulerTrigger))]
    public async Task Run(
        [TimerTrigger("0 0 6 * * *", RunOnStartup = false)] TimerInfo timer,
        [DurableClient] DurableTaskClient client,
        FunctionContext ctx)
    {
        var storageConn = configuration["Storage:ConnectionString"]
            ?? throw new InvalidOperationException("Storage:ConnectionString not configured");

        var configs = new List<NamedPlatformConfig>();

        foreach (var platform in KnownPlatforms)
        {
            var enabled = configuration[$"Platforms:{platform}:Enabled"];
            if (!bool.TryParse(enabled, out bool isEnabled) || !isEnabled) continue;

            var videosPerDay = int.TryParse(configuration[$"Platforms:{platform}:VideosPerDay"],     out int vpd) ? vpd : 1;
            var minSec       = int.TryParse(configuration[$"Platforms:{platform}:VideoLengthSecMin"], out int mn)  ? mn  : 15;
            var maxSec       = int.TryParse(configuration[$"Platforms:{platform}:VideoLengthSecMax"], out int mx)  ? mx  : 18;

            configs.Add(new NamedPlatformConfig(platform, new PlatformConfig(true, videosPerDay, minSec, maxSec)));
            logger.LogInformation("[DailyScheduler] Platform {Platform}: enabled, {Count} videos/day, {Min}-{Max}s",
                platform, videosPerDay, minSec, maxSec);
        }

        if (configs.Count == 0)
        {
            logger.LogWarning("[DailyScheduler] No enabled platforms found in configuration — nothing to schedule.");
            return;
        }

        // UTC date at 6 AM IST = 00:30 UTC = same calendar date as IST
        var date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DailySchedulerOrchestrator),
            new DailySchedulerOrchestratorInput(date, storageConn, configs));

        logger.LogInformation("[DailyScheduler] Started orchestration {Id} for {Date} with {Count} platform(s)",
            instanceId, date, configs.Count);
    }
}
