namespace CarFacts.Functions.Helpers;

/// <summary>
/// Generates US-friendly posting times distributed across typical American activity windows.
/// All times are in UTC. The windows represent when US audiences are most active:
///   • US Morning  (7–10 AM ET)  → UTC 11:00–14:00
///   • US Lunch    (12–2 PM ET)  → UTC 16:00–18:00
///   • US Evening  (5–7 PM ET)   → UTC 21:00–23:00
///   • US Dinner   (8–10 PM ET)  → UTC 00:00–02:00 (+1 day)
///
/// Posts are spread across these windows with ±15 min jitter so no two days look the same.
/// Designed to be platform-agnostic — usable for Twitter, Instagram, LinkedIn, etc.
/// </summary>
public static class UsPostingScheduler
{
    private static readonly (int StartHour, int StartMin, int EndHour, int EndMin, string Name)[] Windows =
    [
        (11, 0, 14, 0, "US Morning"),
        (16, 0, 18, 0, "US Lunch"),
        (21, 0, 23, 0, "US Evening"),
        (0, 0, 2, 0, "US Dinner")     // next day UTC
    ];

    /// <summary>
    /// Generates <paramref name="count"/> randomized US-friendly posting times for the given date.
    /// Times are spread across the 4 windows with ±15 min jitter and a minimum 20-min gap.
    /// </summary>
    public static List<DateTime> GenerateSchedule(DateTime dateUtc, int count)
    {
        if (count <= 0) return [];

        // Seed from date so each day is unique but deterministic within the same orchestration replay
        var seed = dateUtc.Date.DayOfYear * 1000 + dateUtc.Date.Year;
        var rng = new Random(seed);

        // Distribute items round-robin across windows
        var slots = new List<DateTime>();
        for (var i = 0; i < count; i++)
        {
            var window = Windows[i % Windows.Length];
            var baseDate = dateUtc.Date;

            // US Dinner window (0:00–2:00) falls on the next UTC day
            if (window.StartHour < 3)
                baseDate = baseDate.AddDays(1);

            var windowStart = baseDate.AddHours(window.StartHour).AddMinutes(window.StartMin);
            var windowEnd = baseDate.AddHours(window.EndHour).AddMinutes(window.EndMin);
            var windowMinutes = (int)(windowEnd - windowStart).TotalMinutes;

            // Pick a random minute within the window
            var randomMinute = rng.Next(0, windowMinutes);
            var time = windowStart.AddMinutes(randomMinute);

            // Add ±15 min jitter
            var jitter = rng.Next(-15, 16);
            time = time.AddMinutes(jitter);

            // Clamp to not drift too far outside the window
            if (time < windowStart.AddMinutes(-15)) time = windowStart;
            if (time > windowEnd.AddMinutes(15)) time = windowEnd;

            slots.Add(time);
        }

        // Sort chronologically and enforce minimum 20-min gap
        slots.Sort();
        for (var i = 1; i < slots.Count; i++)
        {
            if ((slots[i] - slots[i - 1]).TotalMinutes < 20)
            {
                slots[i] = slots[i - 1].AddMinutes(20 + rng.Next(0, 10));
            }
        }

        return slots;
    }
}
