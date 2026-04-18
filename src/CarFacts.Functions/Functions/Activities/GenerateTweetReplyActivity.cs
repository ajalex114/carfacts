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
        "cars -is:retweet -is:reply lang:en",
        "automobile -is:retweet -is:reply lang:en",
        "automotive -is:retweet -is:reply lang:en",
        "car enthusiast -is:retweet -is:reply lang:en",
        "muscle car -is:retweet -is:reply lang:en",
        "sports car -is:retweet -is:reply lang:en",
        "classic car -is:retweet -is:reply lang:en",
        "electric vehicle -is:retweet -is:reply lang:en"
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

        var tweets = await _twitterService.SearchRecentTweetsAsync(query, maxResults: 20);

        // Filter candidates
        var candidates = tweets
            .Where(t => !string.IsNullOrWhiteSpace(t.Text))
            .Where(t => t.Text.Length >= 20) // skip very short/low-signal tweets
            .Where(t => !t.Text.StartsWith("RT ", StringComparison.OrdinalIgnoreCase))
            .Where(t => !t.AuthorUsername.Equals("carfacts", StringComparison.OrdinalIgnoreCase)) // exclude own account
            .Where(t => t.Text.Count(c => c == '@') <= 2) // skip mention-heavy tweets
            .Where(t => t.Text.Count(c => c == '#') <= 3) // skip hashtag-heavy tweets
            .Where(t => !t.Text.Contains("http://") && !t.Text.Contains("https://")) // skip promo tweets with links
            .Where(t => t.ReplySettings == "everyone") // skip tweets with restricted replies
            .ToList();

        if (candidates.Count == 0)
            throw new InvalidOperationException($"No suitable tweets found for query: {query}");

        // Pick a random candidate
        var selected = candidates[Rng.Next(candidates.Count)];
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
}
