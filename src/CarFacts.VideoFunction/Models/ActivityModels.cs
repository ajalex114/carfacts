namespace CarFacts.VideoFunction.Models;

// ── Input models ─────────────────────────────────────────────────────────────

public record StartVideoRequest(string Fact, string? ImageSearchQuery = null);

public record TtsActivityInput(string JobId, string Fact)
{
    public string StorageConnectionString { get; init; } = "";
}

public record PlanActivityInput(List<WordTiming> Words, double TotalDuration, string Fact, string? ImageSearchQuery = null);

public record FetchClipActivityInput(
    string   JobId,
    int      Index,
    string   SearchQuery,
    double   Duration,
    string   StorageConnectionString,
    string   FfmpegBlobConnectionString,
    ShotType ShotType           = ShotType.ExteriorRolling,
    string?  FallbackQuery      = null,
    string?  BrandOnlyFallback  = null);

public record RenderActivityInput(
    string         JobId,
    string         AudioUrl,
    string         AssSubtitleText,
    List<string?>  ClipUrls,
    double         TotalDuration,
    string         StorageConnectionString,
    List<double>?  SegmentDurations      = null,
    List<ClipSource>? ClipSources        = null);

// ── Output models ────────────────────────────────────────────────────────────

public record TtsActivityResult(
    string         AudioUrl,
    string         AssSubtitleText,
    List<WordTiming> Words,
    double         TotalDuration);

/// <summary>Source info for a single clip — returned in the status API response.</summary>
public record ClipSource(
    int     Index,
    string  Source,   // "Bing/Wikimedia"
    string  Query,
    string? Title = null);

public record FetchClipActivityResult(int Index, string? ClipUrl, string? Attribution = null);

public record RenderActivityResult(string VideoUrl, double DurationSeconds, int ClipCount, List<ClipSource>? ClipSources = null);

public record GenerateQueryActivityInput(string JobId, string Fact);
public record GenerateQueryActivityResult(string Query);

public record GenerateCarFactActivityInput(string JobId);
public record GenerateCarFactActivityResult(string Fact);
