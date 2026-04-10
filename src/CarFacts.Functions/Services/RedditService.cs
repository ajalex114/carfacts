using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CarFacts.Functions.Configuration;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Services;

/// <summary>
/// Posts link submissions to Reddit via the OAuth2 API (script app type).
/// Docs: https://www.reddit.com/dev/api#POST_api_submit
/// </summary>
public sealed class RedditService : ISocialMediaService
{
    private const string TokenEndpoint = "https://www.reddit.com/api/v1/access_token";
    private const string SubmitEndpoint = "https://oauth.reddit.com/api/submit";

    private readonly HttpClient _httpClient;
    private readonly SocialMediaSettings _settings;
    private readonly ISecretProvider _secretProvider;
    private readonly ILogger<RedditService> _logger;

    public string PlatformName => "Reddit";
    public bool IsEnabled => _settings.RedditEnabled;

    public RedditService(
        HttpClient httpClient,
        IOptions<SocialMediaSettings> settings,
        ISecretProvider secretProvider,
        ILogger<RedditService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _secretProvider = secretProvider;
        _logger = logger;
    }

    public async Task PostAsync(string teaser, string postUrl, string postTitle, List<string> keywords, CancellationToken cancellationToken = default)
    {
        if (_settings.RedditSubreddits.Count == 0)
            throw new InvalidOperationException("No subreddits configured in SocialMedia:RedditSubreddits");

        var subreddit = _settings.RedditSubreddits[Random.Shared.Next(_settings.RedditSubreddits.Count)].Trim();
        _logger.LogInformation("Randomly selected subreddit: r/{Sub}", subreddit);

        // Reddit: flair text from first keyword (subreddit must allow it)
        var flairText = keywords.FirstOrDefault() ?? "";

        var accessToken = await AuthenticateAsync(cancellationToken);
        await SubmitLinkAsync(accessToken, subreddit, postTitle, postUrl, flairText, cancellationToken);
    }

    private async Task<string> AuthenticateAsync(CancellationToken cancellationToken)
    {
        var appId = _settings.RedditAppId;
        var appSecret = await _secretProvider.GetSecretAsync(SecretNames.RedditAppSecret, cancellationToken);
        var username = await _secretProvider.GetSecretAsync(SecretNames.RedditUsername, cancellationToken);
        var password = await _secretProvider.GetSecretAsync(SecretNames.RedditPassword, cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{appId}:{appSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.TryAddWithoutValidation("User-Agent", _settings.RedditUserAgent);

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Reddit auth failed ({Status}): {Body}", response.StatusCode, body);
        }
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Reddit auth returned no access_token");
    }

    private async Task SubmitLinkAsync(
        string accessToken, string subreddit, string title, string url, string flairText,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, SubmitEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("User-Agent", _settings.RedditUserAgent);

        var formFields = new Dictionary<string, string>
        {
            ["sr"] = subreddit,
            ["kind"] = "link",
            ["title"] = title,
            ["url"] = url,
            ["resubmit"] = "true"
        };

        if (!string.IsNullOrWhiteSpace(flairText))
            formFields["flair_text"] = flairText;

        request.Content = new FormUrlEncodedContent(formFields);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Reddit submit to r/{Sub} failed ({Status}): {Body}", subreddit, response.StatusCode, body);
        }
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Reddit link posted to r/{Sub}", subreddit);
    }
}
