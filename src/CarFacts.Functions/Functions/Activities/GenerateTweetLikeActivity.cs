using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Activity that searches Twitter for a relevant car-related tweet and returns it for liking.
/// Unlike reply generation, no AI is needed — just find an interesting, relevant tweet.
/// Can like posts, retweets, replies — anything car-related and interesting.
/// </summary>
public sealed class GenerateTweetLikeActivity
{
    private readonly ITwitterService _twitterService;
    private readonly ILogger<GenerateTweetLikeActivity> _logger;
    private static readonly Random Rng = new();

    private static readonly string[] SearchQueries =
    [
        "cars lang:en",
        "automobile lang:en",
        "car enthusiast lang:en",
        "muscle car lang:en",
        "sports car lang:en",
        "classic car lang:en",
        "electric vehicle lang:en",
        "supercar lang:en",
        "car restoration lang:en",
        "car review lang:en",
        "new car launch lang:en",
        "automotive design lang:en"
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

        var tweets = await _twitterService.SearchRecentTweetsAsync(query, maxResults: 30);

        // More permissive filtering than replies — we can like almost anything car-related
        var candidates = tweets
            .Where(t => !string.IsNullOrWhiteSpace(t.Text))
            .Where(t => t.Text.Length >= 15) // skip extremely short tweets
            .Where(t => !t.AuthorUsername.Equals("carfacts", StringComparison.OrdinalIgnoreCase))
            .Where(t => t.Text.Count(c => c == '#') <= 5) // skip spam-heavy tweets
            .ToList();

        if (candidates.Count == 0)
            throw new InvalidOperationException($"No suitable tweets found for liking with query: {query}");

        var selected = candidates[Rng.Next(candidates.Count)];
        _logger.LogInformation("Selected tweet for liking from @{Author}: {Text}",
            selected.AuthorUsername, selected.Text);

        return new TweetLikeResult
        {
            TweetId = selected.TweetId,
            Text = selected.Text,
            AuthorUsername = selected.AuthorUsername
        };
    }
}
