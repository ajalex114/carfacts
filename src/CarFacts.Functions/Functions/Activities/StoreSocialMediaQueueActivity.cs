using CarFacts.Functions.Helpers;
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
            input.LinkTweets.Count,
            input.EnabledPlatforms.Count);

        var items = new List<SocialMediaQueueItem>();

        foreach (var platform in input.EnabledPlatforms)
        {
            // Generate US-friendly posting times for all items on this platform
            var totalItems = input.Facts.Count + input.LinkTweets.Count;
            var schedule = UsPostingScheduler.GenerateSchedule(DateTime.UtcNow, totalItems);
            var scheduleIndex = 0;

            // Add standalone fact tweets
            foreach (var fact in input.Facts)
            {
                var tagLine = string.Join(" ", fact.Hashtags.Take(3));
                var tweetText = $"{fact.Text}\n\n{tagLine}";

                items.Add(new SocialMediaQueueItem
                {
                    Platform = platform,
                    Content = tweetText,
                    Type = "fact",
                    Activity = "post",
                    ScheduledAtUtc = scheduleIndex < schedule.Count ? schedule[scheduleIndex] : null
                });
                scheduleIndex++;
            }

            // Add blog post link tweets
            foreach (var linkTweet in input.LinkTweets)
            {
                var tagLine = string.Join(" ", linkTweet.Hashtags.Take(3));
                var tweetText = $"{linkTweet.Text}\n\n{tagLine}\n\n{linkTweet.PostUrl}";

                items.Add(new SocialMediaQueueItem
                {
                    Platform = platform,
                    Content = tweetText,
                    PostUrl = linkTweet.PostUrl,
                    PostTitle = linkTweet.PostTitle,
                    Type = "link",
                    Activity = "post",
                    ScheduledAtUtc = scheduleIndex < schedule.Count ? schedule[scheduleIndex] : null
                });
                scheduleIndex++;
            }

            if (schedule.Count > 0)
            {
                _logger.LogInformation("Assigned {Count} US-friendly posting times for {Platform}: {Times}",
                    schedule.Count, platform,
                    string.Join(", ", schedule.Select(s => s.ToString("HH:mm 'UTC'"))));
            }
        }

        // Generate reply and like placeholders for Twitter only, gated by config toggles
        if (input.EnabledPlatforms.Any(p => p.Equals("Twitter/X", StringComparison.OrdinalIgnoreCase)))
        {
            var rng = new Random();

            if (input.RepliesEnabled)
            {
                var replyCount = rng.Next(input.RepliesPerDayMin, input.RepliesPerDayMax + 1);
                var postTimes = items
                    .Where(i => i.Platform.Equals("Twitter/X", StringComparison.OrdinalIgnoreCase) && i.ScheduledAtUtc.HasValue)
                    .Select(i => i.ScheduledAtUtc!.Value)
                    .OrderBy(t => t)
                    .ToList();

                var replyTimes = UsPostingScheduler.GenerateInterspersedSlots(postTimes, replyCount);

                foreach (var replyTime in replyTimes)
                {
                    items.Add(new SocialMediaQueueItem
                    {
                        Platform = "Twitter/X",
                        Content = string.Empty,
                        Type = "reply",
                        Activity = "reply",
                        ScheduledAtUtc = replyTime
                    });
                }

                _logger.LogInformation(
                    "Added {ReplyCount} reply slots (range {Min}-{Max}) interspersed at: {Times}",
                    replyTimes.Count, input.RepliesPerDayMin, input.RepliesPerDayMax,
                    string.Join(", ", replyTimes.Select(t => t.ToString("HH:mm 'UTC'"))));
            }
            else
            {
                _logger.LogInformation("Replies disabled via configuration — skipping reply slots");
            }

            if (input.LikesEnabled)
            {
                var likeCount = rng.Next(input.LikesPerDayMin, input.LikesPerDayMax + 1);
                var likeTimes = UsPostingScheduler.GenerateClubbedLikeSlots(DateTime.UtcNow, likeCount);

                foreach (var likeTime in likeTimes)
                {
                    items.Add(new SocialMediaQueueItem
                    {
                        Platform = "Twitter/X",
                        Content = string.Empty,
                        Type = "like",
                        Activity = "like",
                        ScheduledAtUtc = likeTime
                    });
                }

                _logger.LogInformation(
                    "Added {LikeCount} like slots (range {Min}-{Max}, clubbed) at: {Times}",
                    likeTimes.Count, input.LikesPerDayMin, input.LikesPerDayMax,
                    string.Join(", ", likeTimes.Select(t => t.ToString("HH:mm 'UTC'"))));
            }
            else
            {
                _logger.LogInformation("Likes disabled via configuration — skipping like slots");
            }
        }

        await _queueStore.AddItemsAsync(items);

        _logger.LogInformation("Stored {Count} total queue items with scheduled posting times", items.Count);
        return true;
    }
}
