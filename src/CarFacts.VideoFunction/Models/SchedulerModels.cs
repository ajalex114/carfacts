namespace CarFacts.VideoFunction.Models;

/// <summary>Per-platform video generation configuration, loaded from app settings.</summary>
public record PlatformConfig(
    bool Enabled,
    int  VideosPerDay,
    int  VideoLengthSecMin,
    int  VideoLengthSecMax);

/// <summary>A named platform + its config, passed from trigger → orchestrator.</summary>
public record NamedPlatformConfig(string Name, PlatformConfig Config);

/// <summary>
/// A single scheduled-video slot stored in the Cosmos DB <c>video-schedule</c> container.
/// Partition key: /platform (lowercase).
/// </summary>
public class ScheduleEntry
{
    public string Id              { get; set; } = "";
    public string Platform        { get; set; } = "";   // e.g. "youtube"
    public string Date            { get; set; } = "";   // "yyyy-MM-dd" UTC date
    public int    SlotIndex       { get; set; }
    public string ScheduledAt     { get; set; } = "";   // ISO-8601 UTC
    public string Status          { get; set; } = "pending";   // pending | running | completed | failed
    public string? OrchestrationId { get; set; }
    public string CreatedAt       { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    public int    VideoLengthSecMin { get; set; } = 15;
    public int    VideoLengthSecMax { get; set; } = 18;
    public string NarrationStyle    { get; set; } = "";   // e.g. "StyleA-GrandStatement"
}

// ── Activity I/O ────────────────────────────────────────────────────────────

public record GenerateDailyScheduleActivityInput(
    string Platform,
    int    VideosPerDay,
    int    VideoLengthSecMin,
    int    VideoLengthSecMax,
    string Date);

public record GenerateDailyScheduleActivityResult(List<ScheduleEntry> Entries);

// ── Orchestrator inputs ─────────────────────────────────────────────────────

public record DailySchedulerOrchestratorInput(
    string                   Date,
    string                   StorageConnectionString,
    List<NamedPlatformConfig> PlatformConfigs);

public record ScheduledVideoOrchestratorInput(
    string ScheduleEntryId,
    string Platform,
    string ScheduledAt,              // ISO-8601 UTC — Durable timer fires at this time
    string StorageConnectionString,
    int    VideoLengthSecMin,
    int    VideoLengthSecMax,
    string NarrationStyle = "");
