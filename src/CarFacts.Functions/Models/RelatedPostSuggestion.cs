using System.Text.Json.Serialization;

namespace CarFacts.Functions.Models;

/// <summary>
/// A related post suggestion for the "You might also find these interesting" section.
/// Points to a full post (not a specific fact anchor).
/// </summary>
public sealed class RelatedPostSuggestion
{
    [JsonPropertyName("postUrl")]
    public string PostUrl { get; set; } = string.Empty;

    [JsonPropertyName("postTitle")]
    public string PostTitle { get; set; } = string.Empty;

    [JsonPropertyName("imageUrl")]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>Record IDs that contributed to this suggestion (for backlink count increment).</summary>
    [JsonPropertyName("sourceRecordIds")]
    public List<string> SourceRecordIds { get; set; } = [];
}
