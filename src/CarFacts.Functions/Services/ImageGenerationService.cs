using System.Text;
using System.Text.Json;
using CarFacts.Functions.Configuration;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Services;

public sealed class ImageGenerationService : IImageGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly StabilityAISettings _settings;
    private readonly ISecretProvider _secretProvider;
    private readonly ILogger<ImageGenerationService> _logger;

    public ImageGenerationService(
        HttpClient httpClient,
        IOptions<StabilityAISettings> settings,
        ISecretProvider secretProvider,
        ILogger<ImageGenerationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _secretProvider = secretProvider;
        _logger = logger;
    }

    public async Task<List<GeneratedImage>> GenerateImagesAsync(List<CarFact> facts, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating {Count} images via Stability AI", facts.Count);

        var apiKey = await _secretProvider.GetSecretAsync(SecretNames.StabilityAIApiKey, cancellationToken);
        var tasks = facts.Select((fact, index) => GenerateSingleImageAsync(apiKey, fact, index, cancellationToken));

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private async Task<GeneratedImage> GenerateSingleImageAsync(string apiKey, CarFact fact, int index, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating image {Index} for {CarModel} ({Year})", index + 1, fact.CarModel, fact.Year);

        var requestBody = BuildImageRequest(fact);
        var imageBytes = await SendImageRequestAsync(apiKey, requestBody, cancellationToken);

        return new GeneratedImage
        {
            FactIndex = index,
            ImageData = imageBytes,
            FileName = $"car-fact-{fact.Year}-{index + 1}.png"
        };
    }

    private object BuildImageRequest(CarFact fact)
    {
        return new
        {
            text_prompts = new[]
            {
                new
                {
                    text = $"{fact.ImagePrompt}, professional automotive photography, high quality, detailed, photorealistic, 8k resolution",
                    weight = 1
                },
                new
                {
                    text = "blurry, low quality, distorted, text, watermark, logo, signature, ugly, artifacts, cartoon, painting",
                    weight = -1
                }
            },
            cfg_scale = _settings.CfgScale,
            height = _settings.Height,
            width = _settings.Width,
            samples = 1,
            steps = _settings.Steps,
            style_preset = "photographic"
        };
    }

    private async Task<byte[]> SendImageRequestAsync(string apiKey, object requestBody, CancellationToken cancellationToken)
    {
        var url = $"{_settings.BaseUrl.TrimEnd('/')}/v1/generation/{_settings.Model}/text-to-image";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Headers.Add("Accept", "application/json");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return ExtractImageBytes(responseJson);
    }

    private static byte[] ExtractImageBytes(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var base64 = doc.RootElement
            .GetProperty("artifacts")[0]
            .GetProperty("base64")
            .GetString() ?? throw new InvalidOperationException("No image data in Stability AI response");

        return Convert.FromBase64String(base64);
    }
}
