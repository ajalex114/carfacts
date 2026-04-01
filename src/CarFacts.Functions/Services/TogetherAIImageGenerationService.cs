using System.Text;
using System.Text.Json;
using CarFacts.Functions.Configuration;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Services;

/// <summary>
/// Image generation via Together AI API (FLUX, Stable Diffusion, etc.)
/// API: POST https://api.together.xyz/v1/images/generations
/// </summary>
public sealed class TogetherAIImageGenerationService : IImageGenerationService
{
    private const string ApiUrl = "https://api.together.xyz/v1/images/generations";

    private readonly HttpClient _httpClient;
    private readonly TogetherAISettings _settings;
    private readonly ISecretProvider _secretProvider;
    private readonly ILogger<TogetherAIImageGenerationService> _logger;

    public TogetherAIImageGenerationService(
        HttpClient httpClient,
        IOptions<TogetherAISettings> settings,
        ISecretProvider secretProvider,
        ILogger<TogetherAIImageGenerationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _secretProvider = secretProvider;
        _logger = logger;
    }

    public async Task<List<GeneratedImage>> GenerateImagesAsync(
        List<CarFact> facts,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating {Count} images via Together AI ({Model})", facts.Count, _settings.Model);

        var apiKey = await _secretProvider.GetSecretAsync(SecretNames.TogetherAIApiKey, cancellationToken);
        var results = new List<GeneratedImage>();

        foreach (var (fact, index) in facts.Select((f, i) => (f, i)))
        {
            var image = await GenerateSingleImageAsync(apiKey, fact, index, cancellationToken);
            results.Add(image);
        }

        return results;
    }

    private async Task<GeneratedImage> GenerateSingleImageAsync(
        string apiKey, CarFact fact, int index, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating image {Index} for {CarModel} ({Year})", index + 1, fact.CarModel, fact.Year);

        var requestBody = BuildRequest(fact);
        var imageBytes = await SendRequestAsync(apiKey, requestBody, cancellationToken);

        return new GeneratedImage
        {
            FactIndex = index,
            ImageData = imageBytes,
            FileName = $"car-fact-{fact.Year}-{index + 1}.png"
        };
    }

    private object BuildRequest(CarFact fact)
    {
        return new
        {
            model = _settings.Model,
            prompt = $"{fact.ImagePrompt}, professional automotive photography, high quality, detailed, photorealistic, 8k resolution",
            negative_prompt = "blurry, low quality, distorted, text, watermark, logo, signature, ugly, artifacts, cartoon, painting",
            width = _settings.Width,
            height = _settings.Height,
            steps = _settings.Steps,
            n = 1,
            response_format = "b64_json",
            output_format = "png"
        };
    }

    private async Task<byte[]> SendRequestAsync(string apiKey, object requestBody, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Together AI image generation failed ({Status}): {Body}", response.StatusCode, errorBody);
        }
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return ExtractImageBytes(responseJson);
    }

    private static byte[] ExtractImageBytes(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var base64 = doc.RootElement
            .GetProperty("data")[0]
            .GetProperty("b64_json")
            .GetString() ?? throw new InvalidOperationException("No image data in Together AI response");

        return Convert.FromBase64String(base64);
    }
}
