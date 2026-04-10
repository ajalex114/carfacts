using CarFacts.Functions.Models;
using CarFacts.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

public sealed class PublishSocialMediaActivity
{
    private readonly SocialMediaPublisher _publisher;
    private readonly ILogger<PublishSocialMediaActivity> _logger;

    public PublishSocialMediaActivity(
        SocialMediaPublisher publisher,
        ILogger<PublishSocialMediaActivity> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    [Function(nameof(PublishSocialMediaActivity))]
    public async Task<bool> Run(
        [ActivityTrigger] SocialPublishInput input)
    {
        _logger.LogInformation("Publishing to social media for post: {Title}", input.Title);

        try
        {
            await _publisher.PublishAsync(
                input.Teaser, input.PostUrl, input.Title, input.Hashtags);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Social media publishing failed: {Message}", ex.Message);
            return false;
        }
    }
}
