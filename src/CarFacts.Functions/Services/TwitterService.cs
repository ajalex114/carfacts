using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CarFacts.Functions.Configuration;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Services;

/// <summary>
/// Posts tweets via the X (Twitter) API v2 using OAuth 1.0a signature.
/// Docs: https://developer.x.com/en/docs/twitter-api/tweets/manage-tweets/api-reference/post-tweets
/// </summary>
public sealed class TwitterService : ISocialMediaService, ITwitterService
{
    private const string TweetEndpoint = "https://api.twitter.com/2/tweets";
    private const string SearchEndpoint = "https://api.twitter.com/2/tweets/search/recent";
    private const string UsersEndpoint = "https://api.twitter.com/2/users/me";

    private readonly HttpClient _httpClient;
    private readonly SocialMediaSettings _settings;
    private readonly ISecretProvider _secretProvider;
    private readonly ILogger<TwitterService> _logger;

    public string PlatformName => "Twitter/X";
    public bool IsEnabled => _settings.TwitterEnabled;

    public TwitterService(
        HttpClient httpClient,
        IOptions<SocialMediaSettings> settings,
        ISecretProvider secretProvider,
        ILogger<TwitterService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _secretProvider = secretProvider;
        _logger = logger;
    }

    public async Task PostAsync(string teaser, string postUrl, string postTitle, List<string> keywords, CancellationToken cancellationToken = default)
    {
        // Twitter: up to 4 hashtags (already prefixed with # from AI)
        var tagLine = string.Join(" ", keywords.Take(4));

        var tweetText = $"{teaser}\n\n{tagLine}\n\n{postUrl}";
        if (tweetText.Length > 280)
        {
            // Drop tags first, then truncate teaser
            tweetText = $"{teaser}\n\n{postUrl}";
            if (tweetText.Length > 280)
                tweetText = $"{teaser[..(280 - postUrl.Length - 4)]}…\n\n{postUrl}";
        }

        var payload = JsonSerializer.Serialize(new { text = tweetText });

        var consumerKey = await _secretProvider.GetSecretAsync(SecretNames.TwitterConsumerKey, cancellationToken);
        var consumerSecret = await _secretProvider.GetSecretAsync(SecretNames.TwitterConsumerSecret, cancellationToken);
        var accessToken = await _secretProvider.GetSecretAsync(SecretNames.TwitterAccessToken, cancellationToken);
        var accessTokenSecret = await _secretProvider.GetSecretAsync(SecretNames.TwitterAccessTokenSecret, cancellationToken);

        var authHeader = BuildOAuth1Header("POST", TweetEndpoint, consumerKey, consumerSecret, accessToken, accessTokenSecret);

        using var request = new HttpRequestMessage(HttpMethod.Post, TweetEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", authHeader);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Twitter post failed ({Status}): {Body}", response.StatusCode, body);
        }
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Tweet posted successfully");
    }

    public async Task PostRawAsync(string content, CancellationToken cancellationToken = default)
    {
        if (content.Length > 280)
            content = content[..277] + "…";

        var payload = JsonSerializer.Serialize(new { text = content });

        var consumerKey = await _secretProvider.GetSecretAsync(SecretNames.TwitterConsumerKey, cancellationToken);
        var consumerSecret = await _secretProvider.GetSecretAsync(SecretNames.TwitterConsumerSecret, cancellationToken);
        var accessToken = await _secretProvider.GetSecretAsync(SecretNames.TwitterAccessToken, cancellationToken);
        var accessTokenSecret = await _secretProvider.GetSecretAsync(SecretNames.TwitterAccessTokenSecret, cancellationToken);

        var authHeader = BuildOAuth1Header("POST", TweetEndpoint, consumerKey, consumerSecret, accessToken, accessTokenSecret);

        using var request = new HttpRequestMessage(HttpMethod.Post, TweetEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", authHeader);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Twitter raw post failed ({Status}): {Body}", response.StatusCode, body);
        }
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Raw tweet posted successfully");
    }

    public async Task<List<TwitterSearchResult>> SearchRecentTweetsAsync(string query, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        var consumerKey = await _secretProvider.GetSecretAsync(SecretNames.TwitterConsumerKey, cancellationToken);
        var consumerSecret = await _secretProvider.GetSecretAsync(SecretNames.TwitterConsumerSecret, cancellationToken);
        var accessToken = await _secretProvider.GetSecretAsync(SecretNames.TwitterAccessToken, cancellationToken);
        var accessTokenSecret = await _secretProvider.GetSecretAsync(SecretNames.TwitterAccessTokenSecret, cancellationToken);

        var queryParams = new SortedDictionary<string, string>
        {
            ["query"] = query,
            ["max_results"] = maxResults.ToString(),
            ["expansions"] = "author_id",
            ["user.fields"] = "username",
            ["tweet.fields"] = "author_id,text,reply_settings,public_metrics"
        };

        var queryString = string.Join("&", queryParams.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        var url = $"{SearchEndpoint}?{queryString}";

        var authHeader = BuildOAuth1Header("GET", SearchEndpoint, consumerKey, consumerSecret, accessToken, accessTokenSecret, queryParams);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", authHeader);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Twitter search failed ({Status}): {Body}", response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);
        var results = new List<TwitterSearchResult>();

        // Build author_id → username map from includes.users
        var userMap = new Dictionary<string, string>();
        if (doc.RootElement.TryGetProperty("includes", out var includes) &&
            includes.TryGetProperty("users", out var users))
        {
            foreach (var user in users.EnumerateArray())
            {
                var id = user.GetProperty("id").GetString() ?? "";
                var username = user.GetProperty("username").GetString() ?? "";
                userMap[id] = username;
            }
        }

        if (doc.RootElement.TryGetProperty("data", out var data))
        {
            foreach (var tweet in data.EnumerateArray())
            {
                var tweetId = tweet.GetProperty("id").GetString() ?? "";
                var text = tweet.GetProperty("text").GetString() ?? "";
                var authorId = tweet.GetProperty("author_id").GetString() ?? "";
                userMap.TryGetValue(authorId, out var authorUsername);

                var replySettings = tweet.TryGetProperty("reply_settings", out var rs) ? rs.GetString() ?? "everyone" : "everyone";

                int likeCount = 0, retweetCount = 0, replyCount = 0, impressionCount = 0;
                if (tweet.TryGetProperty("public_metrics", out var metrics))
                {
                    likeCount = metrics.TryGetProperty("like_count", out var lc) ? lc.GetInt32() : 0;
                    retweetCount = metrics.TryGetProperty("retweet_count", out var rc) ? rc.GetInt32() : 0;
                    replyCount = metrics.TryGetProperty("reply_count", out var rpc) ? rpc.GetInt32() : 0;
                    impressionCount = metrics.TryGetProperty("impression_count", out var ic) ? ic.GetInt32() : 0;
                }

                results.Add(new TwitterSearchResult
                {
                    TweetId = tweetId,
                    Text = text,
                    AuthorUsername = authorUsername ?? "",
                    ReplySettings = replySettings,
                    LikeCount = likeCount,
                    RetweetCount = retweetCount,
                    ReplyCount = replyCount,
                    ImpressionCount = impressionCount
                });
            }
        }

        _logger.LogInformation("Twitter search returned {Count} tweets for query: {Query}", results.Count, query);
        return results;
    }

    public async Task ReplyToTweetAsync(string tweetId, string content, CancellationToken cancellationToken = default)
    {
        if (content.Length > 280)
            content = content[..277] + "…";

        var payload = JsonSerializer.Serialize(new
        {
            text = content,
            reply = new { in_reply_to_tweet_id = tweetId }
        });

        var consumerKey = await _secretProvider.GetSecretAsync(SecretNames.TwitterConsumerKey, cancellationToken);
        var consumerSecret = await _secretProvider.GetSecretAsync(SecretNames.TwitterConsumerSecret, cancellationToken);
        var accessToken = await _secretProvider.GetSecretAsync(SecretNames.TwitterAccessToken, cancellationToken);
        var accessTokenSecret = await _secretProvider.GetSecretAsync(SecretNames.TwitterAccessTokenSecret, cancellationToken);

        var authHeader = BuildOAuth1Header("POST", TweetEndpoint, consumerKey, consumerSecret, accessToken, accessTokenSecret);

        using var request = new HttpRequestMessage(HttpMethod.Post, TweetEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", authHeader);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Twitter reply failed ({Status}): {Body}", response.StatusCode, body);
        }
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Reply posted to tweet {TweetId}", tweetId);
    }

    public async Task<string> GetAuthenticatedUserIdAsync(CancellationToken cancellationToken = default)
    {
        var consumerKey = await _secretProvider.GetSecretAsync(SecretNames.TwitterConsumerKey, cancellationToken);
        var consumerSecret = await _secretProvider.GetSecretAsync(SecretNames.TwitterConsumerSecret, cancellationToken);
        var accessToken = await _secretProvider.GetSecretAsync(SecretNames.TwitterAccessToken, cancellationToken);
        var accessTokenSecret = await _secretProvider.GetSecretAsync(SecretNames.TwitterAccessTokenSecret, cancellationToken);

        var authHeader = BuildOAuth1Header("GET", UsersEndpoint, consumerKey, consumerSecret, accessToken, accessTokenSecret);

        using var request = new HttpRequestMessage(HttpMethod.Get, UsersEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", authHeader);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Twitter get user failed ({Status}): {Body}", response.StatusCode, body);
        }
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);
        var userId = doc.RootElement.GetProperty("data").GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Could not get authenticated user ID");

        _logger.LogInformation("Authenticated Twitter user ID: {UserId}", userId);
        return userId;
    }

    public async Task LikeTweetAsync(string tweetId, CancellationToken cancellationToken = default)
    {
        var userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        var likeEndpoint = $"https://api.twitter.com/2/users/{userId}/likes";

        var payload = JsonSerializer.Serialize(new { tweet_id = tweetId });

        var consumerKey = await _secretProvider.GetSecretAsync(SecretNames.TwitterConsumerKey, cancellationToken);
        var consumerSecret = await _secretProvider.GetSecretAsync(SecretNames.TwitterConsumerSecret, cancellationToken);
        var accessToken = await _secretProvider.GetSecretAsync(SecretNames.TwitterAccessToken, cancellationToken);
        var accessTokenSecret = await _secretProvider.GetSecretAsync(SecretNames.TwitterAccessTokenSecret, cancellationToken);

        var authHeader = BuildOAuth1Header("POST", likeEndpoint, consumerKey, consumerSecret, accessToken, accessTokenSecret);

        using var request = new HttpRequestMessage(HttpMethod.Post, likeEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", authHeader);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Twitter like failed ({Status}): {Body}", response.StatusCode, body);
        }
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Liked tweet {TweetId}", tweetId);
    }

    private static string BuildOAuth1Header(
        string method, string url,
        string consumerKey, string consumerSecret,
        string token, string tokenSecret,
        SortedDictionary<string, string>? queryParams = null)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "").Replace("/", "").Replace("=", "");

        var parameters = new SortedDictionary<string, string>
        {
            ["oauth_consumer_key"] = consumerKey,
            ["oauth_nonce"] = nonce,
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = timestamp,
            ["oauth_token"] = token,
            ["oauth_version"] = "1.0"
        };

        // For GET requests, query params must be included in the signature base string
        var allParams = new SortedDictionary<string, string>(parameters);
        if (queryParams != null)
        {
            foreach (var kvp in queryParams)
                allParams[kvp.Key] = kvp.Value;
        }

        var paramString = string.Join("&",
            allParams.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        var baseString = $"{method}&{Uri.EscapeDataString(url)}&{Uri.EscapeDataString(paramString)}";
        var signingKey = $"{Uri.EscapeDataString(consumerSecret)}&{Uri.EscapeDataString(tokenSecret)}";

        using var hmac = new HMACSHA1(Encoding.ASCII.GetBytes(signingKey));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.ASCII.GetBytes(baseString)));

        parameters["oauth_signature"] = signature;

        return string.Join(", ",
            parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}=\"{Uri.EscapeDataString(p.Value)}\""));
    }
}
