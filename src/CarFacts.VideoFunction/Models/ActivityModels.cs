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

public record RenderActivityResult(string VideoUrl, double DurationSeconds, int ClipCount, List<ClipSource>? ClipSources = null, string? YouTubeVideoId = null, string? YouTubeVideoUrl = null);

public record GenerateQueryActivityInput(string JobId, string Fact);
public record GenerateQueryActivityResult(string Query);

public record GenerateCarFactActivityInput(string JobId, int VideoLengthSecMin = 15, int VideoLengthSecMax = 18, string NarrationStyle = "");
public record GenerateCarFactActivityResult(string Fact);

/// <summary>
/// Result of the 3-level brand/model selection:
/// Level1 = fresh brand not seen in 5 days,
/// Level2 = fresh model not seen in 5 days (brand was reused),
/// Level3 = oldest/LRU entry (all brands+models seen recently).
/// </summary>
public record BrandModelSelection(string Brand, string? Model, string Reason);

public record PublishToYouTubeActivityInput(string JobId, string Fact, string VideoUrl, string? RelatedVideoUrl = null);
public record PublishToYouTubeActivityResult(string? VideoId, string? VideoUrl, string? Error);

public record GetRelatedVideoActivityInput(string JobId);
public record GetRelatedVideoActivityResult(string? RelatedVideoId, string? RelatedVideoBrand, string? RelatedVideoUrl);

public record SavePublishedVideoActivityInput(
    string JobId,
    string Fact,
    string? YouTubeVideoId,
    string? YouTubeVideoUrl,
    string? RelatedVideoId    = null,
    string? RelatedVideoBrand = null,
    string  Platform          = "YouTube");
public record SavePublishedVideoActivityResult(bool Saved);
