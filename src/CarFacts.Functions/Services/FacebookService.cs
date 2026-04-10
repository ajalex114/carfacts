using System.Text;
using System.Text.Json;
using CarFacts.Functions.Configuration;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Services;

/// <summary>
/// Posts to a Facebook Page via the Graph API.
/// Docs: https://developers.facebook.com/docs/pages-api/posts
/// </summary>
public sealed class FacebookService : ISocialMediaService
{
    private const string GraphApiBase = "https://graph.facebook.com/v21.0";

    private readonly HttpClient _httpClient;
    private readonly SocialMediaSettings _settings;
    private readonly ISecretProvider _secretProvider;
    private readonly ILogger<FacebookService> _logger;

    public string PlatformName => "Facebook";
    public bool IsEnabled => _settings.FacebookEnabled;

    public FacebookService(
        HttpClient httpClient,
        IOptions<SocialMediaSettings> settings,
        ISecretProvider secretProvider,
        ILogger<FacebookService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _secretProvider = secretProvider;
        _logger = logger;
    }

    public async Task PostAsync(string teaser, string postUrl, string postTitle, List<string> keywords, CancellationToken cancellationToken = default)
    {
        var pageId = _settings.FacebookPageIds.Count > 0
            ? _settings.FacebookPageIds[Random.Shared.Next(_settings.FacebookPageIds.Count)].Trim()
            : throw new InvalidOperationException("No Facebook page IDs configured in SocialMedia:FacebookPageIds");

        _logger.LogInformation("Randomly selected Facebook page: {PageId}", pageId);
        var accessToken = await _secretProvider.GetSecretAsync(SecretNames.FacebookPageAccessToken, cancellationToken);

        // Facebook: up to 5 lowercase hashtags
        var hashtags = keywords
            .Take(5)
            .Select(k => k.StartsWith('#') ? k.ToLowerInvariant() : "#" + k.Replace(" ", "").ToLowerInvariant());
        var tagLine = string.Join(" ", hashtags);

        var url = $"{GraphApiBase}/{pageId}/feed";
        var message = $"{teaser}\n\n🔗 Read the full story: {postUrl}\n\n{tagLine}";

        var formData = new Dictionary<string, string>
        {
            ["message"] = message,
            ["link"] = postUrl,
            ["access_token"] = accessToken
        };

        using var response = await _httpClient.PostAsync(url, new FormUrlEncodedContent(formData), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Facebook post failed ({Status}): {Body}", response.StatusCode, body);
        }
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Facebook post published to page {PageId}", pageId);
    }
}
