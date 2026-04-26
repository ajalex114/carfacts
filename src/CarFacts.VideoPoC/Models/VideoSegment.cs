namespace CarFacts.VideoPoC.Models;

/// <summary>One sentence-level segment: time range + search query + resolved local clip path.</summary>
public record VideoSegment(
    string   SearchQuery,
    double   StartSeconds,
    double   EndSeconds)
{
    public double Duration => EndSeconds - StartSeconds;

    /// Set after the clip is downloaded and trimmed.
    public string? ClipPath { get; init; }
}
