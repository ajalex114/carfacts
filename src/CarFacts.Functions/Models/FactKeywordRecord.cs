using System.Text.Json.Serialization;

namespace CarFacts.Functions.Models;

/// <summary>
/// Cosmos DB document representing a single car fact's keywords and URL.
/// Used for cross-post internal backlinking.
/// </summary>
public sealed class FactKeywordRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("postUrl")]
    public string PostUrl { get; set; } = string.Empty;

    [JsonPropertyName("anchorId")]
    public string AnchorId { get; set; } = string.Empty;

    [JsonPropertyName("factUrl")]
    public string FactUrl { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("carModel")]
    public string CarModel { get; set; } = string.Empty;

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = [];

    [JsonPropertyName("backlinkCount")]
    public int BacklinkCount { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
