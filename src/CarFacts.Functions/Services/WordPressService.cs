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
            var media = await UploadSingleImageInternalAsync(authHeader, image, fact, 0, cancellationToken);
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

    public async Task AssociateMediaWithPostAsync(
        List<UploadedMedia> media,
        int postId,
        CancellationToken cancellationToken = default)
    {
        if (media.Count == 0 || postId <= 0) return;

        _logger.LogInformation("Associating {Count} media items with post {PostId}", media.Count, postId);

        var authHeader = await BuildAuthHeaderAsync(cancellationToken);
        var siteId = GetSiteId();

        foreach (var item in media)
        {
            try
            {
                var url = $"{ApiBase}/{siteId}/media/{item.MediaId}";
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = authHeader;

                var body = JsonSerializer.Serialize(new { parent_id = postId });
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Media {MediaId} associated with post {PostId}", item.MediaId, postId);
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Failed to associate media {MediaId} with post {PostId}: {Status} {Body}",
                        item.MediaId, postId, response.StatusCode, errorBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error associating media {MediaId} with post {PostId}", item.MediaId, postId);
            }
        }
    }

    public async Task<WordPressPostResult> CreateDraftPostAsync(
        string title,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating WordPress draft post: {Title}", title);

        var authHeader = await BuildAuthHeaderAsync(cancellationToken);
        var siteId = GetSiteId();
        var url = $"{ApiBase}/{siteId}/posts/new";

        var postBody = new { title, status = "draft", format = "standard" };
        var jsonPayload = JsonSerializer.Serialize(postBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = authHeader;
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("WordPress draft creation failed ({Status}): {Body}", response.StatusCode, errorBody);
        }
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = ParsePostResponse(responseJson);
        _logger.LogInformation("Draft post created: ID={PostId}", result.PostId);
        return result;
    }

    public async Task<WordPressPostResult> UpdateAndPublishPostAsync(
        int postId,
        string title,
        string htmlContent,
        string excerpt,
        int featuredMediaId,
        string seoKeywords,
        string metaDescription,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating and publishing WordPress post {PostId}: {Title}", postId, title);

        var authHeader = await BuildAuthHeaderAsync(cancellationToken);
        var siteId = GetSiteId();
        var url = $"{ApiBase}/{siteId}/posts/{postId}";

        var postBody = new
        {
            title,
            content = htmlContent,
            excerpt,
            status = _settings.PostStatus,
            featured_image = featuredMediaId,
            format = "standard",
            comments_open = true
        };
        var jsonPayload = JsonSerializer.Serialize(postBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = authHeader;
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("WordPress post update failed ({Status}): {Body}", response.StatusCode, errorBody);
        }
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = ParsePostResponse(responseJson);
        _logger.LogInformation("Post published: ID={PostId}, URL={PostUrl}", result.PostId, result.PostUrl);
        return result;
    }

    public async Task<UploadedMedia> UploadSingleImageAsync(
        GeneratedImage image,
        CarFact fact,
        int parentPostId,
        CancellationToken cancellationToken = default)
    {
        var authHeader = await BuildAuthHeaderAsync(cancellationToken);
        return await UploadSingleImageInternalAsync(authHeader, image, fact, parentPostId, cancellationToken);
    }

    public async Task<WordPressPostResult> CreateWebStoryAsync(
        string title,
        string content,
        string excerpt,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating Web Story: {Title}", title);

        var authHeader = await BuildAuthHeaderAsync(cancellationToken);
        var siteId = GetSiteId();
        var url = $"{ApiBase}/{siteId}/posts/new";

        var postBody = new
        {
            title,
            content,
            excerpt,
            status = _settings.PostStatus,
            type = "web-story"
        };
        var jsonPayload = JsonSerializer.Serialize(postBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = authHeader;
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Web Story creation failed ({Status}): {Body}", response.StatusCode, errorBody);
        }
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = ParsePostResponse(responseJson);
        _logger.LogInformation("Web Story published: ID={PostId}, URL={PostUrl}", result.PostId, result.PostUrl);
        return result;
    }

    private async Task<UploadedMedia> UploadSingleImageInternalAsync(
        AuthenticationHeaderValue authHeader,
        GeneratedImage image,
        CarFact fact,
        int parentPostId,
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

        var attrsObj = new Dictionary<string, object>
        {
            ["title"] = $"{fact.CarModel} ({fact.Year})",
            ["caption"] = fact.CatchyTitle,
            ["alt"] = $"{fact.CarModel} from {fact.Year} - historic automotive moment"
        };
        if (parentPostId > 0)
        {
            attrsObj["parent_id"] = parentPostId;
        }
        var attrs = JsonSerializer.Serialize(attrsObj);
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
