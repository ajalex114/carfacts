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

    [JsonPropertyName("postTitle")]
    public string PostTitle { get; set; } = string.Empty;

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

    [JsonPropertyName("imageUrl")]
    public string ImageUrl { get; set; } = string.Empty;

    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = [];

    [JsonPropertyName("backlinkCount")]
    public int BacklinkCount { get; set; }

    [JsonPropertyName("twitterCount")]
    public int TwitterCount { get; set; }

    [JsonPropertyName("mediumCount")]
    public int MediumCount { get; set; }

    [JsonPropertyName("pinterestCount")]
    public int PinterestCount { get; set; }

    /// <summary>Board names this fact has been pinned to (for repost routing).</summary>
    [JsonPropertyName("pinterestBoards")]
    public List<string> PinterestBoards { get; set; } = [];

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
