namespace CarFacts.VideoFunction.Models;

/// <summary>
/// A single news article retrieved from an automotive RSS feed.
/// </summary>
public record NewsItem(
    string          Title,
    string          Summary,
    DateTimeOffset  PublishedAt,
    string          Source,
    string          Url);
