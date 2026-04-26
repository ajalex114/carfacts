namespace CarFacts.VideoFunction.Models;

/// <summary>Camera shot style for a video segment.</summary>
public enum ShotType
{
    ExteriorRolling,  // Car in motion from outside — side/front/rear angle
    InteriorPOV,      // Driver's eye view — steering wheel, dashboard visible
    DroneShot,        // Aerial — top-down or sweeping flyover
    CloseUp           // Detail shot — wheel, grille, badge, headlight
}

/// <summary>One sentence-level segment: time range + search query + resolved local clip path.</summary>
public record VideoSegment(
    string   SearchQuery,
    double   StartSeconds,
    double   EndSeconds,
    ShotType ShotType = ShotType.ExteriorRolling)
{
    public double Duration => EndSeconds - StartSeconds;

    /// Set after the clip is downloaded and trimmed.
    public string? ClipPath { get; init; }

    /// Guaranteed-car fallback query used when the primary search returns no portrait clip.
    public string? FallbackQuery { get; init; }

    /// Brand-only fallback (e.g. "Ford car driving road footage") — used when model-specific
    /// and FallbackQuery both fail on Pexels (Pexels often lacks model-specific content).
    public string? BrandOnlyFallback { get; init; }
}


