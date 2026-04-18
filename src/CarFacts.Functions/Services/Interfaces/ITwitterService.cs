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
}

/// <summary>
/// A tweet returned from the Twitter search API.
/// </summary>
public sealed class TwitterSearchResult
{
    public string TweetId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
}
