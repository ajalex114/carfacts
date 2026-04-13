using CarFacts.Functions.Models;
using CarFacts.Functions.Services;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

public sealed class PublishToMediumActivity
{
    private readonly IMediumService _mediumService;
    private readonly ILogger<PublishToMediumActivity> _logger;

    public PublishToMediumActivity(
        IMediumService mediumService,
        ILogger<PublishToMediumActivity> logger)
    {
        _mediumService = mediumService;
        _logger = logger;
    }

    [Function(nameof(PublishToMediumActivity))]
    public async Task<MediumPublishResult> RunAsync(
        [ActivityTrigger] PublishToMediumInput input)
    {
        if (!_mediumService.IsEnabled)
        {
            _logger.LogInformation("Medium publishing is disabled — skipping");
            return new MediumPublishResult { Success = false };
        }

        _logger.LogInformation("Publishing to Medium: {Title}", input.PostTitle);

        var mediumHtml = MediumContentFormatter.FormatForMedium(
            input.Content,
            input.Seo,
            input.PostUrl,
            input.TodayDate,
            input.Media);

        // Extract up to 3 tags from SEO keywords
        var tags = input.Seo.Keywords.Take(3).ToList();

        var result = await _mediumService.PublishArticleAsync(
            input.PostTitle,
            mediumHtml,
            input.PostUrl,
            tags);

        if (result.Success)
        {
            _logger.LogInformation("Medium article published: {Url}", result.MediumUrl);
        }
        else
        {
            _logger.LogWarning("Medium publish failed for {Title}", input.PostTitle);
        }

        return result;
    }
}
