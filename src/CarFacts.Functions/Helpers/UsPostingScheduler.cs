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

    /// <summary>
    /// Generates <paramref name="count"/> time slots interspersed among existing sorted times.
    /// Picks gaps between existing times and places slots roughly in the middle of each gap.
    /// Ensures no two generated slots are consecutive (always separated by at least one existing post).
    /// </summary>
    public static List<DateTime> GenerateInterspersedSlots(List<DateTime> existingTimes, int count)
    {
        if (existingTimes.Count < 2 || count <= 0) return [];

        var seed = existingTimes[0].DayOfYear * 1000 + existingTimes[0].Year + 7;
        var rng = new Random(seed);

        // Calculate gaps between consecutive existing posts
        var gaps = new List<(int Index, double Minutes)>();
        for (var i = 0; i < existingTimes.Count - 1; i++)
        {
            var gap = (existingTimes[i + 1] - existingTimes[i]).TotalMinutes;
            if (gap >= 30) // only consider gaps large enough to fit a reply
                gaps.Add((i, gap));
        }

        if (gaps.Count == 0) return [];

        // Pick non-consecutive gap indices to ensure replies aren't back-to-back
        var selectedGaps = new List<(int Index, double Minutes)>();
        var usedIndices = new HashSet<int>();

        // Shuffle gaps for randomness
        var shuffled = gaps.OrderBy(_ => rng.Next()).ToList();

        foreach (var gap in shuffled)
        {
            if (selectedGaps.Count >= count) break;

            // Ensure this gap is not adjacent to an already-selected gap
            if (usedIndices.Contains(gap.Index - 1) || usedIndices.Contains(gap.Index + 1))
                continue;

            selectedGaps.Add(gap);
            usedIndices.Add(gap.Index);
        }

        // If we couldn't get enough non-consecutive, fill from remaining
        if (selectedGaps.Count < count)
        {
            foreach (var gap in shuffled)
            {
                if (selectedGaps.Count >= count) break;
                if (usedIndices.Contains(gap.Index)) continue;
                selectedGaps.Add(gap);
                usedIndices.Add(gap.Index);
            }
        }

        // Generate times in the middle of each selected gap with some jitter
        var result = new List<DateTime>();
        foreach (var gap in selectedGaps)
        {
            var start = existingTimes[gap.Index];
            var midpoint = start.AddMinutes(gap.Minutes / 2);
            var jitter = rng.Next(-5, 6);
            result.Add(midpoint.AddMinutes(jitter));
        }

        result.Sort();
        return result;
    }

    /// <summary>
    /// Generates <paramref name="count"/> like slots clubbed in groups of 2-3, spread across US-friendly windows.
    /// Each group fires at the same time (consecutive execution), with gaps between groups.
    /// </summary>
    public static List<DateTime> GenerateClubbedLikeSlots(DateTime dateUtc, int count)
    {
        if (count <= 0) return [];

        var seed = dateUtc.Date.DayOfYear * 1000 + dateUtc.Date.Year + 13;
        var rng = new Random(seed);

        // Determine how many groups and their sizes (2-3 per group)
        var remaining = count;
        var groupSizes = new List<int>();
        while (remaining > 0)
        {
            var size = remaining >= 4 ? rng.Next(2, 4) : remaining; // 2 or 3
            groupSizes.Add(size);
            remaining -= size;
        }

        // Generate one time slot per group, spread across windows
        var groupTimes = new List<DateTime>();
        for (var i = 0; i < groupSizes.Count; i++)
        {
            var window = Windows[i % Windows.Length];
            var baseDate = dateUtc.Date;

            if (window.StartHour < 3)
                baseDate = baseDate.AddDays(1);

            var windowStart = baseDate.AddHours(window.StartHour).AddMinutes(window.StartMin);
            var windowEnd = baseDate.AddHours(window.EndHour).AddMinutes(window.EndMin);
            var windowMinutes = (int)(windowEnd - windowStart).TotalMinutes;

            var randomMinute = rng.Next(0, windowMinutes);
            var time = windowStart.AddMinutes(randomMinute);

            var jitter = rng.Next(-10, 11);
            time = time.AddMinutes(jitter);

            if (time < windowStart.AddMinutes(-10)) time = windowStart;
            if (time > windowEnd.AddMinutes(10)) time = windowEnd;

            groupTimes.Add(time);
        }

        // Sort groups chronologically and enforce minimum 15-min gap between groups
        groupTimes.Sort();
        for (var i = 1; i < groupTimes.Count; i++)
        {
            if ((groupTimes[i] - groupTimes[i - 1]).TotalMinutes < 15)
                groupTimes[i] = groupTimes[i - 1].AddMinutes(15 + rng.Next(0, 5));
        }

        // Expand groups: each like in a club is 30 seconds apart (consecutive execution)
        var slots = new List<DateTime>();
        for (var g = 0; g < groupSizes.Count; g++)
        {
            for (var j = 0; j < groupSizes[g]; j++)
            {
                slots.Add(groupTimes[g].AddSeconds(j * 30));
            }
        }

        return slots;
    }
}
