using CarFacts.Functions.Configuration;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarFacts.Functions.Functions;

public sealed class DailyCarFactsFunction
{
    private readonly IContentGenerationService _contentGenerator;
    private readonly IImageGenerationService _imageGenerator;
    private readonly IWordPressService _wordPressService;
    private readonly IContentFormatterService _contentFormatter;
    private readonly WordPressSettings _wpSettings;
    private readonly ILogger<DailyCarFactsFunction> _logger;

    public DailyCarFactsFunction(
        IContentGenerationService contentGenerator,
        IImageGenerationService imageGenerator,
        IWordPressService wordPressService,
        IContentFormatterService contentFormatter,
        IOptions<WordPressSettings> wpSettings,
        ILogger<DailyCarFactsFunction> logger)
    {
        _contentGenerator = contentGenerator;
        _imageGenerator = imageGenerator;
        _wordPressService = wordPressService;
        _contentFormatter = contentFormatter;
        _wpSettings = wpSettings.Value;
        _logger = logger;
    }

    [Function(nameof(DailyCarFactsFunction))]
    public async Task RunAsync(
        [TimerTrigger("%Schedule:CronExpression%")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var todayDate = DateTime.UtcNow.ToString("MMMM d");
        _logger.LogInformation("Car Facts pipeline started for {Date}", todayDate);

        var response = await GenerateContentAsync(todayDate, cancellationToken);

        if (_wpSettings.SkipImages)
        {
            await PublishTextOnlyAsync(response, todayDate, cancellationToken);
        }
        else
        {
            var images = await GenerateImagesAsync(response, cancellationToken);

            if (images.Count == 0)
            {
                _logger.LogWarning("All image providers failed — publishing text-only");
                await PublishTextOnlyAsync(response, todayDate, cancellationToken);
            }
            else if (_wpSettings.EmbedImagesAsBase64)
            {
                _logger.LogInformation("Step 3/4: Skipping media upload — embedding images as base64");
                var htmlContent = _contentFormatter.FormatPostHtmlWithBase64Images(response, images, todayDate);
                await CreatePostAsync(response, htmlContent, 0, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Step 3/4: Uploading images to WordPress");
                var media = await _wordPressService.UploadImagesAsync(images, response.Facts, cancellationToken);
                var htmlContent = _contentFormatter.FormatPostHtml(response, media, todayDate);
                var featuredMediaId = media.FirstOrDefault()?.MediaId ?? 0;
                await CreatePostAsync(response, htmlContent, featuredMediaId, cancellationToken);
            }
        }
    }

    private async Task PublishTextOnlyAsync(
        Models.CarFactsResponse response,
        string todayDate,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Publishing text-only (no images)");
        var htmlContent = _contentFormatter.FormatPostHtml(response, new List<Models.UploadedMedia>(), todayDate);
        await CreatePostAsync(response, htmlContent, 0, cancellationToken);
    }

    private async Task CreatePostAsync(
        Models.CarFactsResponse response,
        string htmlContent,
        int featuredMediaId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Step 4/4: Publishing post to WordPress");

        SaveHtmlDebugFile(htmlContent);

        var result = await _wordPressService.CreatePostAsync(
            response.MainTitle, htmlContent, response.MetaDescription,
            featuredMediaId, string.Join(", ", response.Keywords),
            response.MetaDescription, cancellationToken);

        _logger.LogInformation("✅ Published: {Title} → {Url} (ID: {PostId})", result.Title, result.PostUrl, result.PostId);
    }

    private static void SaveHtmlDebugFile(string htmlContent)
    {
        var isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
        if (isAzure) return;

        var logsDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "logs");
        Directory.CreateDirectory(logsDir);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var path = Path.Combine(logsDir, $"post-{timestamp}.html");
        File.WriteAllText(path, htmlContent);
    }

    private async Task<Models.CarFactsResponse> GenerateContentAsync(string todayDate, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Step 1/4: Generating content via Azure OpenAI");
        return await _contentGenerator.GenerateFactsAsync(todayDate, cancellationToken);
    }

    private async Task<List<Models.GeneratedImage>> GenerateImagesAsync(Models.CarFactsResponse response, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Step 2/4: Generating {Count} images via Stability AI", response.Facts.Count);
        return await _imageGenerator.GenerateImagesAsync(response.Facts, cancellationToken);
    }
}
