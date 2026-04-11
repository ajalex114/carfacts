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
    public async Task<TweetLinkResult?> Run(
        [ActivityTrigger] GenerateTweetLinkInput input)
    {
        _logger.LogInformation("Generating tweet link for post: {Title}", input.PostTitle);

        // Select which post to tweet — weighted by lower twitterCount then backlinkCount
        var selectedPostUrl = input.PostUrl;
        var selectedPostTitle = input.PostTitle;

        var allRecords = await _factStore.GetAllPostRecordsAsync();
        if (allRecords.Count > 0)
        {
            // Group by postUrl, compute weights
            var postGroups = allRecords
                .Where(r => !string.IsNullOrEmpty(r.PostUrl) && !string.IsNullOrEmpty(r.PostTitle))
                .GroupBy(r => r.PostUrl)
                .Select(g =>
                {
                    var records = g.ToList();
                    var totalTwitter = records.Sum(r => r.TwitterCount);
                    var totalBacklinks = records.Sum(r => r.BacklinkCount);
                    var best = records.First();
                    return new
                    {
                        PostUrl = g.Key,
                        PostTitle = best.PostTitle,
                        TotalTwitter = totalTwitter,
                        TotalBacklinks = totalBacklinks
                    };
                })
                .ToList();

            if (postGroups.Count > 0)
            {
                // Weighted random: 1/(twitterCount+1) * 1/(backlinkCount+1)
                var weights = postGroups.Select(g =>
                    1.0 / (g.TotalTwitter + 1) * 1.0 / (g.TotalBacklinks + 1)).ToList();
                var totalWeight = weights.Sum();
                var roll = Rng.NextDouble() * totalWeight;
                var cumulative = 0.0;

                for (int i = 0; i < postGroups.Count; i++)
                {
                    cumulative += weights[i];
                    if (roll <= cumulative)
                    {
                        selectedPostUrl = postGroups[i].PostUrl;
                        selectedPostTitle = postGroups[i].PostTitle;
                        _logger.LogInformation("Selected post for tweet: {Title} (twitter={Tc}, backlink={Bc})",
                            selectedPostTitle, postGroups[i].TotalTwitter, postGroups[i].TotalBacklinks);
                        break;
                    }
                }
            }
        }

        // Generate the hook tweet via OpenAI
        var history = new ChatHistory();
        history.AddSystemMessage(PromptLoader.LoadTweetFactsSystemPrompt());
        history.AddUserMessage(PromptLoader.LoadTweetLinkPrompt(selectedPostTitle, selectedPostUrl));

        var response = await _chatService.GetChatMessageContentAsync(history);
        var content = response.Content
            ?? throw new InvalidOperationException("Empty AI response for tweet link");

        var cleaned = CleanJson(content);
        var parsed = JsonSerializer.Deserialize<TweetLinkJson>(cleaned)
            ?? throw new InvalidOperationException("Failed to deserialize tweet link");

        _logger.LogInformation("Generated link tweet for: {Url}", selectedPostUrl);

        return new TweetLinkResult
        {
            Text = parsed.Text,
            Hashtags = parsed.Hashtags,
            PostUrl = selectedPostUrl,
            PostTitle = selectedPostTitle
        };
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
