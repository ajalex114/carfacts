using System.Text.Json;
using CarFacts.Functions.Models;
using CarFacts.Functions.Prompts;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;

namespace CarFacts.Functions.Functions.Activities;

public sealed class GenerateTweetLinkActivity
{
    private readonly IChatCompletionService _chatService;
    private readonly IFactKeywordStore _factStore;
    private readonly ILogger<GenerateTweetLinkActivity> _logger;
    private static readonly Random Rng = new();

    public GenerateTweetLinkActivity(
        IChatCompletionService chatService,
        IFactKeywordStore factStore,
        ILogger<GenerateTweetLinkActivity> logger)
    {
        _chatService = chatService;
        _factStore = factStore;
        _logger = logger;
    }

    [Function(nameof(GenerateTweetLinkActivity))]
    public async Task<List<TweetLinkResult>> Run(
        [ActivityTrigger] GenerateTweetLinkInput input)
    {
        var linkCount = Math.Max(1, input.LinkCount);
        _logger.LogInformation("Generating {Count} tweet link(s) for post: {Title}", linkCount, input.PostTitle);

        // Build weighted post list from Cosmos DB
        var candidates = new List<(string PostUrl, string PostTitle, int TotalTwitter, int TotalBacklinks)>();
        var allRecords = await _factStore.GetAllPostRecordsAsync();
        if (allRecords.Count > 0)
        {
            candidates = allRecords
                .Where(r => !string.IsNullOrEmpty(r.PostUrl) && !string.IsNullOrEmpty(r.PostTitle))
                .GroupBy(r => r.PostUrl)
                .Select(g =>
                {
                    var records = g.ToList();
                    return (
                        PostUrl: g.Key,
                        PostTitle: records.First().PostTitle,
                        TotalTwitter: records.Sum(r => r.TwitterCount),
                        TotalBacklinks: records.Sum(r => r.BacklinkCount)
                    );
                })
                .ToList();
        }

        // Select distinct posts using weighted random (without replacement)
        var selectedPosts = SelectWeightedPosts(candidates, linkCount, input.PostUrl, input.PostTitle);

        // Generate a hook tweet for each selected post
        var results = new List<TweetLinkResult>();
        foreach (var post in selectedPosts)
        {
            var history = new ChatHistory();
            history.AddSystemMessage(PromptLoader.LoadTweetFactsSystemPrompt());
            history.AddUserMessage(PromptLoader.LoadTweetLinkPrompt(post.PostTitle, post.PostUrl));

            var response = await _chatService.GetChatMessageContentAsync(history);
            var content = response.Content
                ?? throw new InvalidOperationException("Empty AI response for tweet link");

            var cleaned = CleanJson(content);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<TweetLinkJson>(cleaned, options)
                ?? throw new InvalidOperationException("Failed to deserialize tweet link");

            _logger.LogInformation("Generated link tweet for: {Url}", post.PostUrl);

            results.Add(new TweetLinkResult
            {
                Text = parsed.Text,
                Hashtags = parsed.Hashtags,
                PostUrl = post.PostUrl,
                PostTitle = post.PostTitle
            });
        }

        return results;
    }

    private static List<(string PostUrl, string PostTitle)> SelectWeightedPosts(
        List<(string PostUrl, string PostTitle, int TotalTwitter, int TotalBacklinks)> candidates,
        int count,
        string fallbackUrl,
        string fallbackTitle)
    {
        var selected = new List<(string PostUrl, string PostTitle)>();
        var remaining = candidates.ToList();

        for (int n = 0; n < count; n++)
        {
            if (remaining.Count == 0)
            {
                // Fallback to the current day's post if we run out of candidates
                if (!selected.Any(s => s.PostUrl == fallbackUrl))
                    selected.Add((fallbackUrl, fallbackTitle));
                break;
            }

            var weights = remaining.Select(g =>
                1.0 / (g.TotalTwitter + 1) * 1.0 / (g.TotalBacklinks + 1)).ToList();
            var totalWeight = weights.Sum();
            var roll = Rng.NextDouble() * totalWeight;
            var cumulative = 0.0;

            for (int i = 0; i < remaining.Count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                {
                    selected.Add((remaining[i].PostUrl, remaining[i].PostTitle));
                    remaining.RemoveAt(i);
                    break;
                }
            }
        }

        return selected;
    }

    private static string CleanJson(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;
        return trimmed.Replace("```json", "").Replace("```", "").Trim();
    }

    private sealed class TweetLinkJson
    {
        public string Text { get; set; } = string.Empty;
        public List<string> Hashtags { get; set; } = [];
    }
}
