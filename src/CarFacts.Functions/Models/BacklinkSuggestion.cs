using System.Text.Json.Serialization;

namespace CarFacts.Functions.Models;

/// <summary>
/// A backlink suggestion for a specific fact, pointing to a related fact from a previous post.
/// </summary>
public sealed class BacklinkSuggestion
{
    [JsonPropertyName("factIndex")]
    public int FactIndex { get; set; }

    [JsonPropertyName("targetFactUrl")]
    public string TargetFactUrl { get; set; } = string.Empty;

    [JsonPropertyName("targetTitle")]
    public string TargetTitle { get; set; } = string.Empty;

    [JsonPropertyName("targetCarModel")]
    public string TargetCarModel { get; set; } = string.Empty;

    [JsonPropertyName("targetYear")]
    public int TargetYear { get; set; }

    [JsonPropertyName("targetRecordId")]
    public string TargetRecordId { get; set; } = string.Empty;
}
