using CarFacts.VideoFunction.Models;
using CarFacts.VideoFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Activities;

/// <summary>
/// Generates N random video-publication times spread evenly through the day for a given platform,
/// then persists them to the Cosmos DB <c>video-schedule</c> container.
///
/// Schedule window: 6:10 AM → 11:50 PM IST (= 00:40 → 18:20 UTC on the same date).
/// The day is divided into <c>VideosPerDay</c> equal slots; one random time is chosen per slot
/// so videos are evenly distributed without clustering.
///
/// Must be an activity (not inline in orchestrator) because it uses Random — non-deterministic.
/// </summary>
public class GenerateDailyScheduleActivity(
    VideoScheduleService scheduleService,
    ILogger<GenerateDailyScheduleActivity> logger)
{
    // IST = UTC+5:30 → 6:10 AM IST = 00:40 UTC, 11:50 PM IST = 18:20 UTC
    private static readonly TimeSpan DayStartUtc = TimeSpan.FromMinutes(40);   // 00:40 UTC
    private static readonly TimeSpan DayEndUtc   = TimeSpan.FromMinutes(18 * 60 + 20); // 18:20 UTC

    [Function(nameof(GenerateDailyScheduleActivity))]
    public async Task<GenerateDailyScheduleActivityResult> Run(
        [ActivityTrigger] GenerateDailyScheduleActivityInput input,
        FunctionContext ctx)
    {
        var date     = DateOnly.Parse(input.Date);
        var dayStart = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero) + DayStartUtc;
        var dayEnd   = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero) + DayEndUtc;
        var totalMinutes = (int)(dayEnd - dayStart).TotalMinutes;       // 1060 min
        var slotMinutes  = totalMinutes / input.VideosPerDay;           // e.g. 53 min per slot

        var entries = new List<ScheduleEntry>(input.VideosPerDay);
        var platformLower = input.Platform.ToLowerInvariant();

        for (var i = 0; i < input.VideosPerDay; i++)
        {
            // Pick a random minute within this slot
            var slotOffset  = i * slotMinutes + Random.Shared.Next(0, slotMinutes);
            var scheduledAt = dayStart.AddMinutes(slotOffset);

            entries.Add(new ScheduleEntry
            {
                Id               = $"{input.Date}-{platformLower}-{i:D2}",
                Platform         = platformLower,
                Date             = input.Date,
                SlotIndex        = i,
                ScheduledAt      = scheduledAt.ToString("O"),
                Status           = "pending",
                CreatedAt        = DateTimeOffset.UtcNow.ToString("O"),
                VideoLengthSecMin = input.VideoLengthSecMin,
                VideoLengthSecMax = input.VideoLengthSecMax,
                NarrationStyle   = NarrationStyles.ForSlot(i).Name
            });
        }

        logger.LogInformation("[{Platform}] Generated {Count} schedule slots for {Date}. First={First}, Last={Last}",
            input.Platform, entries.Count, input.Date,
            entries.First().ScheduledAt, entries.Last().ScheduledAt);

        await scheduleService.SaveScheduleEntriesAsync(entries);

        return new GenerateDailyScheduleActivityResult(entries);
    }
}
