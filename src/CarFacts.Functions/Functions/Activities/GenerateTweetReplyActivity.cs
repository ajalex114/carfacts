using System.Text.Json;
using CarFacts.Functions.Models;
using CarFacts.Functions.Prompts;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Activity that searches Twitter for a recent car-related tweet and generates
/// a human-like reply using AI. Returns the reply text and target tweet info.
/// </summary>
public sealed class GenerateTweetReplyActivity
{
    private readonly ITwitterService _twitterService;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<GenerateTweetReplyActivity> _logger;
    private static readonly Random Rng = new();

    private static readonly string[] SearchQueries =
    [
        "Tesla -is:retweet -is:reply lang:en",
        "BMW -is:retweet -is:reply lang:en",
        "Mercedes -is:retweet -is:reply lang:en",
        "Porsche -is:retweet -is:reply lang:en",
        "Ferrari -is:retweet -is:reply lang:en",
        "Lamborghini -is:retweet -is:reply lang:en",
        "Ford Mustang -is:retweet -is:reply lang:en",
        "Corvette -is:retweet -is:reply lang:en",
        "Toyota Supra -is:retweet -is:reply lang:en",
        "McLaren -is:retweet -is:reply lang:en",
        "Audi -is:retweet -is:reply lang:en",
        "Dodge Challenger -is:retweet -is:reply lang:en"
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

    public GenerateTweetReplyActivity(
        ITwitterService twitterService,
        IChatCompletionService chatService,
        ILogger<GenerateTweetReplyActivity> logger)
    {
        _twitterService = twitterService;
        _chatService = chatService;
        _logger = logger;
    }

    [Function(nameof(GenerateTweetReplyActivity))]
    public async Task<TweetReplyResult> Run(
        [ActivityTrigger] string trigger)
    {
        // Pick a random search query for variety
        var query = SearchQueries[Rng.Next(SearchQueries.Length)];
        _logger.LogInformation("Searching Twitter with query: {Query}", query);

        var tweets = await _twitterService.SearchRecentTweetsAsync(query, maxResults: 100);

        // Filter candidates — high engagement + car brand relevance
        var candidates = tweets
            .Where(t => !string.IsNullOrWhiteSpace(t.Text))
            .Where(t => t.Text.Length >= 20)
            .Where(t => !t.Text.StartsWith("RT ", StringComparison.OrdinalIgnoreCase))
            .Where(t => !t.AuthorUsername.Equals("carfacts", StringComparison.OrdinalIgnoreCase))
            .Where(t => t.Text.Count(c => c == '@') <= 2)
            .Where(t => t.Text.Count(c => c == '#') <= 3)
            .Where(t => !t.Text.Contains("http://") && !t.Text.Contains("https://"))
            .Where(t => t.ReplySettings == "everyone")
            .Where(t => t.ImpressionCount >= 2000)
            .Where(t => t.LikeCount >= 100)
            .Where(t => t.RetweetCount >= 2)
            .Where(t => t.ReplyCount >= 50)
            .Where(t => ContainsCarBrand(t.Text))
            .OrderByDescending(t => t.TotalEngagement)
            .ToList();

        _logger.LogInformation(
            "Reply candidates after engagement filter: {Count} out of {Total} tweets",
            candidates.Count, tweets.Count);

        // Relaxed fallback: lower engagement thresholds but still require car brand
        if (candidates.Count == 0)
        {
            candidates = tweets
                .Where(t => !string.IsNullOrWhiteSpace(t.Text))
                .Where(t => t.Text.Length >= 20)
                .Where(t => !t.Text.StartsWith("RT ", StringComparison.OrdinalIgnoreCase))
                .Where(t => !t.AuthorUsername.Equals("carfacts", StringComparison.OrdinalIgnoreCase))
                .Where(t => t.ReplySettings == "everyone")
                .Where(t => t.LikeCount >= 20)
                .Where(t => t.RetweetCount >= 1)
                .Where(t => ContainsCarBrand(t.Text))
                .OrderByDescending(t => t.TotalEngagement)
                .ToList();

            _logger.LogInformation("Relaxed reply filter yielded {Count} candidates", candidates.Count);
        }

        if (candidates.Count == 0)
            throw new InvalidOperationException($"No high-engagement car tweets found for reply with query: {query}");

        // Pick from top engaged candidates
        var pool = candidates.Take(Math.Max(3, candidates.Count / 2)).ToList();
        var selected = pool[Rng.Next(pool.Count)];
        _logger.LogInformation("Selected tweet from @{Author}: {Text}", selected.AuthorUsername, selected.Text);

        // Generate a reply using AI
        var history = new ChatHistory();
        history.AddSystemMessage(PromptLoader.LoadTweetReplySystemPrompt());
        history.AddUserMessage(PromptLoader.LoadTweetReplyUserPrompt(selected.Text));

        var response = await _chatService.GetChatMessageContentAsync(history);
        var content = response.Content
            ?? throw new InvalidOperationException("Empty AI response for tweet reply");

        var cleaned = CleanJson(content);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var parsed = JsonSerializer.Deserialize<TweetReplyJson>(cleaned, options)
            ?? throw new InvalidOperationException("Failed to deserialize tweet reply");

        _logger.LogInformation("Generated reply to @{Author}: {Reply}", selected.AuthorUsername, parsed.Reply);

        return new TweetReplyResult
        {
            TweetId = selected.TweetId,
            OriginalText = selected.Text,
            ReplyText = parsed.Reply,
            AuthorUsername = selected.AuthorUsername
        };
    }

    private static string CleanJson(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;
        return trimmed.Replace("```json", "").Replace("```", "").Trim();
    }

    private sealed class TweetReplyJson
    {
        public string Reply { get; set; } = string.Empty;
    }

    private static bool ContainsCarBrand(string text)
    {
        var lower = text.ToLowerInvariant();
        return CarBrands.Any(brand => lower.Contains(brand));
    }
}
