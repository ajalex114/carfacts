using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarFacts.Functions.Configuration;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Services;

/// <summary>
/// Pinterest API v5 client using OAuth2 Bearer token.
/// Docs: https://developers.pinterest.com/docs/api/v5/
/// </summary>
public sealed class PinterestService : IPinterestService
{
    private const string BaseUrl = "https://api.pinterest.com/v5";

    private readonly HttpClient _httpClient;
    private readonly ISecretProvider _secretProvider;
    private readonly ILogger<PinterestService> _logger;

    // Cache board name → ID to avoid repeated API calls
    private readonly Dictionary<string, string> _boardCache = new(StringComparer.OrdinalIgnoreCase);

    public PinterestService(
        HttpClient httpClient,
        ISecretProvider secretProvider,
        ILogger<PinterestService> logger)
    {
        _httpClient = httpClient;
        _secretProvider = secretProvider;
        _logger = logger;
    }

    public async Task<string> CreatePinAsync(
        string boardId,
        string title,
        string description,
        string link,
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            board_id = boardId,
            title = title.Length > 100 ? title[..100] : title,
            description = description.Length > 500 ? description[..500] : description,
            link,
            alt_text = title,
            media_source = new
            {
                source_type = "image_url",
                url = imageUrl
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var response = await SendAsync(HttpMethod.Post, "/pins", json, cancellationToken);

        var result = JsonSerializer.Deserialize<PinterestPinResponse>(response, JsonOptions);
        _logger.LogInformation("Pinterest pin created: {PinId} on board {BoardId}", result?.Id, boardId);
        return result?.Id ?? string.Empty;
    }

    public async Task<List<(string Id, string Name)>> ListBoardsAsync(CancellationToken cancellationToken = default)
    {
        var boards = new List<(string Id, string Name)>();
        string? bookmark = null;

        do
        {
            var url = bookmark != null ? $"/boards?bookmark={bookmark}" : "/boards";
            var response = await SendAsync(HttpMethod.Get, url, null, cancellationToken);
            var result = JsonSerializer.Deserialize<PinterestListResponse<PinterestBoardResponse>>(response, JsonOptions);

            if (result?.Items != null)
            {
                foreach (var board in result.Items)
                {
                    boards.Add((board.Id, board.Name));
                    _boardCache[board.Name] = board.Id;
                }
            }

            bookmark = result?.Bookmark;
        } while (!string.IsNullOrEmpty(bookmark));

        _logger.LogInformation("Listed {Count} Pinterest boards", boards.Count);
        return boards;
    }

    public async Task<string> CreateBoardAsync(string name, string description, CancellationToken cancellationToken = default)
    {
        var payload = new { name, description, privacy = "PUBLIC" };
        var json = JsonSerializer.Serialize(payload);
        var response = await SendAsync(HttpMethod.Post, "/boards", json, cancellationToken);

        var result = JsonSerializer.Deserialize<PinterestBoardResponse>(response, JsonOptions);
        var boardId = result?.Id ?? string.Empty;

        if (!string.IsNullOrEmpty(boardId))
            _boardCache[name] = boardId;

        _logger.LogInformation("Created Pinterest board '{Name}': {BoardId}", name, boardId);
        return boardId;
    }

    public async Task<string> GetOrCreateBoardAsync(string boardName, CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_boardCache.TryGetValue(boardName, out var cachedId))
            return cachedId;

        // List boards and populate cache
        await ListBoardsAsync(cancellationToken);

        if (_boardCache.TryGetValue(boardName, out var existingId))
            return existingId;

        // Board doesn't exist — create it
        return await CreateBoardAsync(
            boardName,
            $"Daily car facts about {boardName.ToLowerInvariant()}",
            cancellationToken);
    }

    private async Task<string> SendAsync(HttpMethod method, string path, string? body, CancellationToken cancellationToken)
    {
        var token = await _secretProvider.GetSecretAsync(SecretNames.PinterestAccessToken, cancellationToken);

        using var request = new HttpRequestMessage(method, $"{BaseUrl}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body != null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Pinterest API error ({Status}): {Body}", response.StatusCode, responseBody);
            response.EnsureSuccessStatusCode();
        }

        return responseBody;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class PinterestPinResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }

    private sealed class PinterestBoardResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class PinterestListResponse<T>
    {
        [JsonPropertyName("items")]
        public List<T> Items { get; set; } = [];

        [JsonPropertyName("bookmark")]
        public string? Bookmark { get; set; }
    }
}
