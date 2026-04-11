using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

public sealed class StoreSocialMediaQueueActivity
{
    private readonly ISocialMediaQueueStore _queueStore;
    private readonly ILogger<StoreSocialMediaQueueActivity> _logger;

    public StoreSocialMediaQueueActivity(
        ISocialMediaQueueStore queueStore,
        ILogger<StoreSocialMediaQueueActivity> logger)
    {
        _queueStore = queueStore;
        _logger = logger;
    }

    [Function(nameof(StoreSocialMediaQueueActivity))]
    public async Task<bool> Run(
        [ActivityTrigger] StoreSocialQueueInput input)
    {
        _logger.LogInformation("Storing {FactCount} facts + {LinkCount} link tweets for {PlatformCount} platforms",
            input.Facts.Count,
            input.LinkTweet != null ? 1 : 0,
            input.EnabledPlatforms.Count);

        var items = new List<SocialMediaQueueItem>();

        foreach (var platform in input.EnabledPlatforms)
        {
            // Add standalone fact tweets
            foreach (var fact in input.Facts)
            {
                var tagLine = string.Join(" ", fact.Hashtags.Take(3));
                var tweetText = $"{fact.Text}\n\n{tagLine}";

                items.Add(new SocialMediaQueueItem
                {
                    Platform = platform,
                    Content = tweetText,
                    Type = "fact"
                });
            }

            // Add blog post link tweet
            if (input.LinkTweet != null)
            {
                var tagLine = string.Join(" ", input.LinkTweet.Hashtags.Take(3));
                var tweetText = $"{input.LinkTweet.Text}\n\n{tagLine}\n\n{input.LinkTweet.PostUrl}";

                items.Add(new SocialMediaQueueItem
                {
                    Platform = platform,
                    Content = tweetText,
                    PostUrl = input.LinkTweet.PostUrl,
                    PostTitle = input.LinkTweet.PostTitle,
                    Type = "link"
                });
            }
        }

        await _queueStore.AddItemsAsync(items);

        _logger.LogInformation("Stored {Count} total queue items", items.Count);
        return true;
    }
}
