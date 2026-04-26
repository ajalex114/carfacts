namespace CarFacts.VideoFunction.Models;

// ── Input models ─────────────────────────────────────────────────────────────

public record StartVideoRequest(string Fact);

public record TtsActivityInput(string JobId, string Fact)
{
    public string StorageConnectionString { get; init; } = "";
}

public record PlanActivityInput(List<WordTiming> Words, double TotalDuration, string Fact);

public record FetchClipActivityInput(
    string   JobId,
    int      Index,
    string   SearchQuery,
    double   Duration,
    string   PexelsApiKey,
    string   StorageConnectionString,
    string   FfmpegBlobConnectionString,
    string   YouTubeApiKey,
    string   VisionEndpoint,
    string   VisionApiKey,
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
    string  Source,   // "YouTube CC" | "Pexels"
    string  Query,
    string? Title = null);

public record FetchClipActivityResult(int Index, string? ClipUrl, string? Attribution = null);

public record RenderActivityResult(string VideoUrl, double DurationSeconds, int ClipCount, List<ClipSource>? ClipSources = null);
