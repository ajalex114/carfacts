using System.Text.Json.Serialization;

namespace CarFacts.Functions.Models;

/// <summary>
/// Full post document stored in the Cosmos DB 'posts' container.
/// This is the canonical source of truth for the Azure Static Web App.
/// </summary>
public sealed class PostDocument
{
    /// <summary>Document ID: "{yyyy-MM-dd}_{slug}"</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Partition key: "{yyyy-MM}" — partitions by month for efficient range queries.</summary>
    [JsonPropertyName("partitionKey")]
    public string PartitionKey { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Canonical SWA URL: https://carfactsdaily.com/YYYY/MM/DD/slug/</summary>
    [JsonPropertyName("postUrl")]
    public string PostUrl { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("metaDescription")]
    public string MetaDescription { get; set; } = string.Empty;

    [JsonPropertyName("excerpt")]
    public string Excerpt { get; set; } = string.Empty;

    [JsonPropertyName("htmlContent")]
    public string HtmlContent { get; set; } = string.Empty;

    [JsonPropertyName("featuredImageUrl")]
    public string FeaturedImageUrl { get; set; } = string.Empty;

    [JsonPropertyName("images")]
    public List<PostImage> Images { get; set; } = [];

    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = [];

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("socialHashtags")]
    public List<string> SocialHashtags { get; set; } = [];

    [JsonPropertyName("category")]
    public string Category { get; set; } = "car-facts";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "thecargeek";

    [JsonPropertyName("publishedAt")]
    public DateTime PublishedAt { get; set; }

    [JsonPropertyName("geoSummary")]
    public string GeoSummary { get; set; } = string.Empty;

    [JsonPropertyName("facts")]
    public List<CarFact> Facts { get; set; } = [];

    /// <summary>WordPress post ID — for reference during migration only.</summary>
    [JsonPropertyName("wordPressPostId")]
    public int WordPressPostId { get; set; }

    /// <summary>WordPress post URL — for reference during migration only.</summary>
    [JsonPropertyName("wordPressPostUrl")]
    public string WordPressPostUrl { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Image metadata for a post image stored in Azure Blob Storage.</summary>
public sealed class PostImage
{
    [JsonPropertyName("factIndex")]
    public int FactIndex { get; set; }

    [JsonPropertyName("blobUrl")]
    public string BlobUrl { get; set; } = string.Empty;

    [JsonPropertyName("blobPath")]
    public string BlobPath { get; set; } = string.Empty;

    [JsonPropertyName("altText")]
    public string AltText { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("caption")]
    public string Caption { get; set; } = string.Empty;
}

/// <summary>
/// Lightweight summary used for sitemap generation and RSS feed construction.
/// </summary>
public sealed class PostSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("postUrl")]
    public string PostUrl { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("metaDescription")]
    public string MetaDescription { get; set; } = string.Empty;

    [JsonPropertyName("excerpt")]
    public string Excerpt { get; set; } = string.Empty;

    [JsonPropertyName("featuredImageUrl")]
    public string FeaturedImageUrl { get; set; } = string.Empty;

    [JsonPropertyName("publishedAt")]
    public DateTime PublishedAt { get; set; }
}
