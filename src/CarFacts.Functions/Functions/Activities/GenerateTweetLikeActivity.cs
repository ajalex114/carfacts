using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Activity that searches Twitter for a relevant car-related tweet and returns it for liking.
/// Selects tweets with high engagement (2000+ views, 100+ likes, 50+ comments) from
/// globally renowned car brands and car-related content.
/// </summary>
public sealed class GenerateTweetLikeActivity
{
    private readonly ITwitterService _twitterService;
    private readonly ILogger<GenerateTweetLikeActivity> _logger;
    private static readonly Random Rng = new();

    // Search queries targeting popular car brands and automotive content
    private static readonly string[] SearchQueries =
    [
        "Tesla -is:retweet lang:en",
        "BMW -is:retweet lang:en",
        "Mercedes -is:retweet lang:en",
        "Porsche -is:retweet lang:en",
        "Ferrari -is:retweet lang:en",
        "Lamborghini -is:retweet lang:en",
        "Ford Mustang -is:retweet lang:en",
        "Corvette -is:retweet lang:en",
        "Toyota Supra -is:retweet lang:en",
        "McLaren -is:retweet lang:en",
        "Audi -is:retweet lang:en",
        "Bugatti -is:retweet lang:en",
        "Dodge Challenger -is:retweet lang:en",
        "Rolls Royce car -is:retweet lang:en",
        "Aston Martin -is:retweet lang:en",
        "Pagani -is:retweet lang:en"
    ];

    // Car brands for relevance validation
    private static readonly string[] CarBrands =
    [
        "tesla", "bmw", "mercedes", "porsche", "ferrari", "lamborghini", "ford",
        "mustang", "corvette", "toyota", "mclaren", "audi", "bugatti", "dodge",
        "rolls royce", "aston martin", "pagani", "honda", "nissan", "chevrolet",
        "chevy", "lexus", "bentley", "maserati", "jaguar", "range rover",
        "land rover", "subaru", "mazda", "hyundai", "kia", "volkswagen", "vw",
        "volvo", "cadillac", "lincoln", "genesis", "acura", "infiniti",
        "alfa romeo", "lotus", "koenigsegg", "rimac", "lucid", "rivian",
        "shelby", "camaro", "hellcat", "supra", "gt-r", "gtr", "m3", "m4", "m5",
        "rs6", "rs7", "amg", "911"
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

        var tweets = await _twitterService.SearchRecentTweetsAsync(query, maxResults: 100);

        // Filter for high-engagement, car-relevant tweets
        var candidates = tweets
            .Where(t => !string.IsNullOrWhiteSpace(t.Text))
            .Where(t => t.Text.Length >= 20)
            .Where(t => !t.AuthorUsername.Equals("carfacts", StringComparison.OrdinalIgnoreCase))
            .Where(t => t.Text.Count(c => c == '#') <= 5)
            .Where(t => t.ImpressionCount >= 2000)
            .Where(t => t.LikeCount >= 100)
            .Where(t => t.RetweetCount >= 2)
            .Where(t => t.ReplyCount >= 50)
            .Where(t => ContainsCarBrand(t.Text))
            .OrderByDescending(t => t.TotalEngagement)
            .ToList();

        _logger.LogInformation(
            "Like candidates after engagement filter: {Count} out of {Total} tweets",
            candidates.Count, tweets.Count);

        // If strict filter yields nothing, relax to just engagement + car brand
        if (candidates.Count == 0)
        {
            candidates = tweets
                .Where(t => !string.IsNullOrWhiteSpace(t.Text))
                .Where(t => t.Text.Length >= 20)
                .Where(t => !t.AuthorUsername.Equals("carfacts", StringComparison.OrdinalIgnoreCase))
                .Where(t => t.LikeCount >= 20)
                .Where(t => t.RetweetCount >= 1)
                .Where(t => ContainsCarBrand(t.Text))
                .OrderByDescending(t => t.TotalEngagement)
                .ToList();

            _logger.LogInformation("Relaxed filter yielded {Count} candidates", candidates.Count);
        }

        if (candidates.Count == 0)
            throw new InvalidOperationException($"No high-engagement car tweets found for query: {query}");

        // Pick from top half for variety
        var pool = candidates.Take(Math.Max(3, candidates.Count / 2)).ToList();
        var selected = pool[Rng.Next(pool.Count)];

        _logger.LogInformation(
            "Selected tweet for liking from @{Author} ({Views} views, {Likes}♥, {RTs}🔁, {Replies}💬): {Text}",
            selected.AuthorUsername, selected.ImpressionCount, selected.LikeCount,
            selected.RetweetCount, selected.ReplyCount,
            selected.Text.Length > 100 ? selected.Text[..100] + "..." : selected.Text);

        return new TweetLikeResult
        {
            TweetId = selected.TweetId,
            Text = selected.Text,
            AuthorUsername = selected.AuthorUsername
        };
    }

    private static bool ContainsCarBrand(string text)
    {
        var lower = text.ToLowerInvariant();
        return CarBrands.Any(brand => lower.Contains(brand));
    }
}
