using System.Text.Json.Serialization;

namespace CarFacts.Functions.Models;

/// <summary>
/// SEO metadata generated from a separate LLM pass
/// that has full visibility of all content.
/// </summary>
public sealed class SeoMetadata
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
}
