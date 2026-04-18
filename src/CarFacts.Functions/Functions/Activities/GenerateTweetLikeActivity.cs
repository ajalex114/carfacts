using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Activity that searches Twitter for a relevant car-related tweet and returns it for liking.
/// Selects tweets with above-average engagement to interact with quality content.
/// </summary>
public sealed class GenerateTweetLikeActivity
{
    private readonly ITwitterService _twitterService;
    private readonly ILogger<GenerateTweetLikeActivity> _logger;
    private static readonly Random Rng = new();

    private static readonly string[] SearchQueries =
    [
        "car review -is:retweet lang:en",
        "muscle car -is:retweet lang:en",
        "sports car -is:retweet lang:en",
        "classic car -is:retweet lang:en",
        "electric vehicle review lang:en",
        "supercar -is:retweet lang:en",
        "car restoration -is:retweet lang:en",
        "new car launch lang:en",
        "automotive design lang:en",
        "car collection -is:retweet lang:en",
        "car enthusiast -is:retweet lang:en",
        "car history -is:retweet lang:en"
    ];

    public GenerateTweetLikeActivity(
        ITwitterService twitterService,
        ILogger<GenerateTweetLikeActivity> logger)
    {
        _twitterService = twitterService;
        _logger = logger;
    }

    [Function(nameof(GenerateTweetLikeActivity))]
    public async Task<TweetLikeResult> Run(
        [ActivityTrigger] string trigger)
    {
        var query = SearchQueries[Rng.Next(SearchQueries.Length)];
        _logger.LogInformation("Searching Twitter for likeable car tweet with query: {Query}", query);

        var tweets = await _twitterService.SearchRecentTweetsAsync(query, maxResults: 50);

        // Basic quality filters
        var candidates = tweets
            .Where(t => !string.IsNullOrWhiteSpace(t.Text))
            .Where(t => t.Text.Length >= 20)
            .Where(t => !t.AuthorUsername.Equals("carfacts", StringComparison.OrdinalIgnoreCase))
            .Where(t => t.Text.Count(c => c == '#') <= 5)
            .ToList();

        if (candidates.Count == 0)
            throw new InvalidOperationException($"No suitable tweets found for liking with query: {query}");

        // Compute average engagement across candidates, then pick from above-average
        var avgEngagement = candidates.Average(t => t.TotalEngagement);
        var engagedCandidates = candidates
            .Where(t => t.TotalEngagement >= avgEngagement && t.TotalEngagement >= 2)
            .OrderByDescending(t => t.TotalEngagement)
            .ToList();

        _logger.LogInformation(
            "Like candidates: {Total} total, avg engagement {Avg:F1}, {Engaged} above average",
            candidates.Count, avgEngagement, engagedCandidates.Count);

        // If no above-average candidates, fall back to top 3 by engagement
        if (engagedCandidates.Count == 0)
            engagedCandidates = candidates.OrderByDescending(t => t.TotalEngagement).Take(3).ToList();

        // Pick randomly from the top engaged candidates (top half for variety)
        var pool = engagedCandidates.Take(Math.Max(3, engagedCandidates.Count / 2)).ToList();
        var selected = pool[Rng.Next(pool.Count)];

        _logger.LogInformation(
            "Selected tweet for liking from @{Author} (engagement: {Likes}♥ {RTs}🔁 {Replies}💬): {Text}",
            selected.AuthorUsername, selected.LikeCount, selected.RetweetCount, selected.ReplyCount, selected.Text);

        return new TweetLikeResult
        {
            TweetId = selected.TweetId,
            Text = selected.Text,
            AuthorUsername = selected.AuthorUsername
        };
    }
}
