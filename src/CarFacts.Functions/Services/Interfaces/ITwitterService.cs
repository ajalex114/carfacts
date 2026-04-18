namespace CarFacts.Functions.Services.Interfaces;

/// <summary>
/// Twitter-specific operations beyond basic posting (search, reply).
/// Separated from ISocialMediaService since these are platform-specific.
/// </summary>
public interface ITwitterService
{
    /// <summary>
    /// Searches for recent tweets matching the given query using Twitter API v2.
    /// </summary>
    Task<List<TwitterSearchResult>> SearchRecentTweetsAsync(string query, int maxResults = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a reply to a specific tweet using Twitter API v2.
    /// </summary>
    Task ReplyToTweetAsync(string tweetId, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Likes a tweet on behalf of the authenticated user using Twitter API v2.
    /// </summary>
    Task LikeTweetAsync(string tweetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the authenticated user's Twitter ID (needed for like/unlike endpoints).
    /// </summary>
    Task<string> GetAuthenticatedUserIdAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A tweet returned from the Twitter search API.
/// </summary>
public sealed class TwitterSearchResult
{
    public string TweetId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
    /// <summary>"everyone", "mentionedUsers", or "following" — indicates who can reply.</summary>
    public string ReplySettings { get; set; } = "everyone";

    // Engagement metrics from public_metrics
    public int LikeCount { get; set; }
    public int RetweetCount { get; set; }
    public int ReplyCount { get; set; }
    public int ImpressionCount { get; set; }

    /// <summary>Total engagement = likes + retweets + replies.</summary>
    public int TotalEngagement => LikeCount + RetweetCount + ReplyCount;
}
