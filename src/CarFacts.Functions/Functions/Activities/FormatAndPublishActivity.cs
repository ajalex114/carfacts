using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Formats the HTML content for the blog post.
/// Returns the formatted HTML string for saving to Cosmos DB.
/// </summary>
public sealed class FormatAndPublishActivity
{
    private readonly IContentFormatterService _formatterService;
    private readonly ILogger<FormatAndPublishActivity> _logger;

    public FormatAndPublishActivity(
        IContentFormatterService formatterService,
        ILogger<FormatAndPublishActivity> logger)
    {
        _formatterService = formatterService;
        _logger = logger;
    }

    [Function(nameof(FormatAndPublishActivity))]
    public Task<string> Run(
        [ActivityTrigger] PublishInput input)
    {
        _logger.LogInformation("Formatting post HTML for {Date}", input.TodayDate);

        var htmlContent = _formatterService.FormatPostHtml(
            input.Content, input.Seo, input.Media, input.TodayDate, input.Backlinks, input.RelatedPosts);

        _logger.LogInformation("HTML formatted successfully ({Length} chars)", htmlContent.Length);

        return Task.FromResult(htmlContent);
    }
}
