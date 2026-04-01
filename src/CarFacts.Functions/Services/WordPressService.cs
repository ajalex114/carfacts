using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CarFacts.Functions.Configuration;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Services;

/// <summary>
/// WordPress.com REST API v1.1 integration using OAuth2 Bearer token.
/// API docs: https://developer.wordpress.com/docs/api/
/// </summary>
public sealed class WordPressService : IWordPressService
{
    private const string ApiBase = "https://public-api.wordpress.com/rest/v1.1/sites";

    private readonly HttpClient _httpClient;
    private readonly WordPressSettings _settings;
    private readonly ISecretProvider _secretProvider;
    private readonly ILogger<WordPressService> _logger;

    public WordPressService(
        HttpClient httpClient,
        IOptions<WordPressSettings> settings,
        ISecretProvider secretProvider,
        ILogger<WordPressService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _secretProvider = secretProvider;
        _logger = logger;
    }

    public async Task<List<UploadedMedia>> UploadImagesAsync(
        List<GeneratedImage> images,
        List<CarFact> facts,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Uploading {Count} images to WordPress.com", images.Count);

        var authHeader = await BuildAuthHeaderAsync(cancellationToken);
        var results = new List<UploadedMedia>();

        foreach (var image in images)
        {
            var fact = facts[image.FactIndex];
            var media = await UploadSingleImageAsync(authHeader, image, fact, cancellationToken);
            results.Add(media);
        }

        return results;
    }

    public async Task<WordPressPostResult> CreatePostAsync(
        string title,
        string htmlContent,
        string excerpt,
        int featuredMediaId,
        string seoKeywords,
        string metaDescription,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating WordPress.com post: {Title}", title);

        var authHeader = await BuildAuthHeaderAsync(cancellationToken);
        var postBody = BuildPostBody(title, htmlContent, excerpt, featuredMediaId);

        return await SendCreatePostRequestAsync(authHeader, postBody, cancellationToken);
    }

    private async Task<UploadedMedia> UploadSingleImageAsync(
        AuthenticationHeaderValue authHeader,
        GeneratedImage image,
        CarFact fact,
        CancellationToken cancellationToken)
    {
        var siteId = GetSiteId();
        var url = $"{ApiBase}/{siteId}/media/new";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = authHeader;

        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(image.ImageData);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "media[]", image.FileName);

        var attrs = JsonSerializer.Serialize(new
        {
            title = $"{fact.CarModel} ({fact.Year})",
            caption = fact.CatchyTitle,
            alt = $"{fact.CarModel} from {fact.Year} - historic automotive moment"
        });
        content.Add(new StringContent(attrs, Encoding.UTF8, "application/json"), "attrs[]");
        request.Content = content;

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("WordPress media upload failed ({Status}): {Body}", response.StatusCode, errorBody);
        }
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseMediaResponse(responseJson, image.FactIndex);
    }

    private static UploadedMedia ParseMediaResponse(string responseJson, int factIndex)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var media = doc.RootElement.GetProperty("media")[0];

        return new UploadedMedia
        {
            FactIndex = factIndex,
            MediaId = media.GetProperty("ID").GetInt32(),
            SourceUrl = media.GetProperty("URL").GetString() ?? string.Empty
        };
    }

    private object BuildPostBody(string title, string htmlContent, string excerpt, int featuredMediaId)
    {
        return new
        {
            title,
            content = htmlContent,
            excerpt,
            status = _settings.PostStatus,
            featured_image = featuredMediaId,
            format = "standard",
            comments_open = true
        };
    }

    private async Task<WordPressPostResult> SendCreatePostRequestAsync(
        AuthenticationHeaderValue authHeader,
        object postBody,
        CancellationToken cancellationToken)
    {
        var siteId = GetSiteId();
        var url = $"{ApiBase}/{siteId}/posts/new";
        var jsonPayload = JsonSerializer.Serialize(postBody);

        _logger.LogDebug("WordPress post payload: {Payload}", jsonPayload);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = authHeader;
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("WordPress post creation failed ({Status}): {Body}", response.StatusCode, errorBody);
        }
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParsePostResponse(responseJson);
    }

    private static WordPressPostResult ParsePostResponse(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        return new WordPressPostResult
        {
            PostId = root.GetProperty("ID").GetInt32(),
            PostUrl = root.GetProperty("URL").GetString() ?? string.Empty,
            Title = root.GetProperty("title").GetString() ?? string.Empty,
            PublishedAt = root.GetProperty("date").GetDateTime()
        };
    }

    private string GetSiteId()
    {
        return _settings.SiteId;
    }

    private async Task<AuthenticationHeaderValue> BuildAuthHeaderAsync(CancellationToken cancellationToken)
    {
        var token = await _secretProvider.GetSecretAsync(SecretNames.WordPressOAuthToken, cancellationToken);
        return new AuthenticationHeaderValue("Bearer", token);
    }
}
