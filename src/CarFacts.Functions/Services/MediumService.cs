using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CarFacts.Functions.Configuration;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Services;

public sealed class MediumService : IMediumService
{
    private const string ApiBase = "https://api.medium.com/v1";

    private readonly HttpClient _httpClient;
    private readonly MediumSettings _settings;
    private readonly ISecretProvider _secretProvider;
    private readonly ILogger<MediumService> _logger;

    public MediumService(
        HttpClient httpClient,
        IOptions<MediumSettings> settings,
        ISecretProvider secretProvider,
        ILogger<MediumService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _secretProvider = secretProvider;
        _logger = logger;
    }

    public bool IsEnabled => _settings.Enabled;

    public async Task<MediumPublishResult> PublishArticleAsync(
        string title,
        string htmlContent,
        string canonicalUrl,
        List<string> tags,
        CancellationToken cancellationToken = default)
    {
        var token = await _secretProvider.GetSecretAsync(SecretNames.MediumIntegrationToken, cancellationToken);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Medium integration token not found in Key Vault");
            return new MediumPublishResult { Success = false };
        }

        // Get authenticated user's ID
        var authorId = await GetAuthorIdAsync(token, cancellationToken);
        if (string.IsNullOrEmpty(authorId))
        {
            _logger.LogError("Failed to get Medium author ID");
            return new MediumPublishResult { Success = false };
        }

        // Publish the article
        var url = $"{ApiBase}/users/{authorId}/posts";

        var postBody = new
        {
            title,
            contentFormat = "html",
            content = $"<h1>{System.Web.HttpUtility.HtmlEncode(title)}</h1>{htmlContent}",
            canonicalUrl,
            tags = tags.Take(3).ToList(),
            publishStatus = "public"
        };

        var jsonPayload = JsonSerializer.Serialize(postBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Medium publish failed ({Status}): {Body}", response.StatusCode, responseBody);
            return new MediumPublishResult { Success = false };
        }

        using var doc = JsonDocument.Parse(responseBody);
        var data = doc.RootElement.GetProperty("data");
        var mediumUrl = data.GetProperty("url").GetString() ?? "";
        var mediumId = data.GetProperty("id").GetString() ?? "";

        _logger.LogInformation("Published to Medium: {Url} (id={Id})", mediumUrl, mediumId);

        return new MediumPublishResult
        {
            Success = true,
            MediumUrl = mediumUrl,
            MediumPostId = mediumId
        };
    }

    private async Task<string?> GetAuthorIdAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Medium /me failed ({Status}): {Body}", response.StatusCode, body);
            return null;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement.GetProperty("data").GetProperty("id").GetString();
    }
}
