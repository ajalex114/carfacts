using CarFacts.VideoFunction.Activities;
using CarFacts.VideoFunction.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Functions;

/// <summary>
/// Daily scheduling orchestrator — started once per day by DailySchedulerTrigger at 6 AM IST.
///
/// For each enabled platform it:
///   1. Calls GenerateDailyScheduleActivity to produce N random publication times and persist them.
///   2. Fans out one ScheduledVideoOrchestrator sub-orchestration per slot.
///
/// The orchestrator stays alive until the last video of the day completes (~midnight IST).
/// Each sub-orchestration is independent — a failure in one does not affect the others.
/// </summary>
public class DailySchedulerOrchestrator
{
    [Function(nameof(DailySchedulerOrchestrator))]
    public async Task Run([OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var logger = ctx.CreateReplaySafeLogger<DailySchedulerOrchestrator>();
        var input  = ctx.GetInput<DailySchedulerOrchestratorInput>()
            ?? throw new InvalidOperationException("DailySchedulerOrchestrator: missing input");

        logger.LogInformation("[DailyScheduler] Orchestrator started for {Date} with {Count} platform(s)",
            input.Date, input.PlatformConfigs.Count);

        var allTasks = new List<Task>();

        foreach (var namedConfig in input.PlatformConfigs)
        {
            if (!namedConfig.Config.Enabled) continue;

            // Generate (and persist) the randomised schedule for this platform
            var scheduleResult = await ctx.CallActivityAsync<GenerateDailyScheduleActivityResult>(
                nameof(GenerateDailyScheduleActivity),
                new GenerateDailyScheduleActivityInput(
                    namedConfig.Name,
                    namedConfig.Config.VideosPerDay,
                    namedConfig.Config.VideoLengthSecMin,
                    namedConfig.Config.VideoLengthSecMax,
                    input.Date));

            logger.LogInformation("[DailyScheduler] [{Platform}] {Count} slots generated",
                namedConfig.Name, scheduleResult.Entries.Count);

            // Fan out: one sub-orchestration per slot, isolated error handling
            foreach (var entry in scheduleResult.Entries)
            {
                var subInput = new ScheduledVideoOrchestratorInput(
                    entry.Id,
                    namedConfig.Name,
                    entry.ScheduledAt,
                    input.StorageConnectionString,
                    namedConfig.Config.VideoLengthSecMin,
                    namedConfig.Config.VideoLengthSecMax,
                    entry.NarrationStyle);

                // Use a stable instance ID so the slot is identifiable in the Durable dashboard
                var subInstanceId = $"sched-{entry.Id}";

                allTasks.Add(SafeCallSubOrchestrationAsync(ctx, logger, subInstanceId, subInput));
            }
        }

        await Task.WhenAll(allTasks);

        logger.LogInformation("[DailyScheduler] All {Count} sub-orchestrations completed for {Date}",
            allTasks.Count, input.Date);
    }

    /// <summary>
    /// Calls a ScheduledVideoOrchestrator sub-orchestration and swallows any exception so that
    /// one failed video does not cancel the remaining slots for the day.
    /// </summary>
    private static async Task SafeCallSubOrchestrationAsync(
        TaskOrchestrationContext ctx,
        Microsoft.Extensions.Logging.ILogger logger,
        string instanceId,
        ScheduledVideoOrchestratorInput input)
    {
        try
        {
            await ctx.CallSubOrchestratorAsync<string?>(
                nameof(ScheduledVideoOrchestrator),
                input,
                new SubOrchestrationOptions { InstanceId = instanceId });
        }
        catch (Exception ex)
        {
            logger.LogWarning("[DailyScheduler] Slot {EntryId} failed (isolated): {Error}",
                input.ScheduleEntryId, ex.Message);
        }
    }
}
