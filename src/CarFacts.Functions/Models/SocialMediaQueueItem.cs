using System.Text.Json.Serialization;

namespace CarFacts.Functions.Models;

/// <summary>
/// Cosmos DB document representing a queued social media post.
/// Stored in the social-media-queue container, consumed and deleted by the posting trigger.
/// </summary>
public sealed class SocialMediaQueueItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>Only populated for "link" type items.</summary>
    [JsonPropertyName("postUrl")]
    public string? PostUrl { get; set; }

    /// <summary>Only populated for "link" type items.</summary>
    [JsonPropertyName("postTitle")]
    public string? PostTitle { get; set; }

    /// <summary>"fact" for standalone car facts, "link" for blog post promotions.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "fact";

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>TTL in seconds — 48 hours. Cosmos DB auto-deletes expired items.</summary>
    [JsonPropertyName("ttl")]
    public int Ttl { get; set; } = 172800;
}
