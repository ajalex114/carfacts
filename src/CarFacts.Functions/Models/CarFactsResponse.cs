using System.Text.Json.Serialization;

namespace CarFacts.Functions.Models;

public sealed class CarFactsResponse
{
    [JsonPropertyName("main_title")]
    public string MainTitle { get; set; } = string.Empty;

    [JsonPropertyName("meta_description")]
    public string MetaDescription { get; set; } = string.Empty;

    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = [];

    [JsonPropertyName("geo_summary")]
    public string GeoSummary { get; set; } = string.Empty;

    [JsonPropertyName("social_media_teaser")]
    public string SocialMediaTeaser { get; set; } = string.Empty;

    [JsonPropertyName("social_media_hashtags")]
    public List<string> SocialMediaHashtags { get; set; } = [];

    [JsonPropertyName("facts")]
    public List<CarFact> Facts { get; set; } = [];
}
