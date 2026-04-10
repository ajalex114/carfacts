using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Formats the HTML content and publishes the WordPress post.
/// If DraftPostId > 0, updates and publishes the existing draft.
/// Otherwise, creates a new post directly.
/// </summary>
public sealed class FormatAndPublishActivity
{
    private readonly IContentFormatterService _formatterService;
    private readonly IWordPressService _wordPressService;
    private readonly ILogger<FormatAndPublishActivity> _logger;

    public FormatAndPublishActivity(
        IContentFormatterService formatterService,
        IWordPressService wordPressService,
        ILogger<FormatAndPublishActivity> logger)
    {
        _formatterService = formatterService;
        _wordPressService = wordPressService;
        _logger = logger;
    }

    [Function(nameof(FormatAndPublishActivity))]
    public async Task<WordPressPostResult> Run(
        [ActivityTrigger] PublishInput input)
    {
        _logger.LogInformation("Formatting and publishing post for {Date}", input.TodayDate);

        var htmlContent = _formatterService.FormatPostHtml(
            input.Content, input.Seo, input.Media, input.TodayDate, input.Backlinks, input.RelatedPosts);

        var featuredMediaId = input.Media.FirstOrDefault()?.MediaId ?? 0;
        var keywords = string.Join(", ", input.Seo.Keywords);

        WordPressPostResult result;

        if (input.DraftPostId > 0)
        {
            result = await _wordPressService.UpdateAndPublishPostAsync(
                input.DraftPostId,
                input.Seo.MainTitle,
                htmlContent,
                input.Seo.SocialMediaTeaser,
                featuredMediaId,
                keywords,
                input.Seo.MetaDescription);
        }
        else
        {
            result = await _wordPressService.CreatePostAsync(
                input.Seo.MainTitle,
                htmlContent,
                input.Seo.SocialMediaTeaser,
                featuredMediaId,
                keywords,
                input.Seo.MetaDescription);
        }

        _logger.LogInformation("Post published: {PostUrl}", result.PostUrl);
        return result;
    }
}
