using System.Text.Json;
using CarFacts.Functions.Models;
using CarFacts.Functions.Prompts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;

namespace CarFacts.Functions.Functions.Activities;

public sealed class GenerateTweetFactsActivity
{
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<GenerateTweetFactsActivity> _logger;

    public GenerateTweetFactsActivity(
        IChatCompletionService chatService,
        ILogger<GenerateTweetFactsActivity> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [Function(nameof(GenerateTweetFactsActivity))]
    public async Task<List<TweetFactResult>> Run(
        [ActivityTrigger] int factsCount)
    {
        _logger.LogInformation("Generating {Count} standalone tweet facts", factsCount);

        var history = new ChatHistory();
        history.AddSystemMessage(PromptLoader.LoadTweetFactsSystemPrompt());
        history.AddUserMessage(PromptLoader.LoadTweetFactsUserPrompt(factsCount));

        var response = await _chatService.GetChatMessageContentAsync(history);
        var content = response.Content
            ?? throw new InvalidOperationException("Empty AI response for tweet facts");

        var cleaned = CleanJson(content);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var parsed = JsonSerializer.Deserialize<TweetFactsResponse>(cleaned, options)
            ?? throw new InvalidOperationException("Failed to deserialize tweet facts");

        _logger.LogInformation("Generated {Count} tweet facts", parsed.Tweets.Count);
        return parsed.Tweets.Select(t => new TweetFactResult
        {
            Text = t.Text,
            Hashtags = t.Hashtags
        }).ToList();
    }

    private static string CleanJson(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;
        return trimmed.Replace("```json", "").Replace("```", "").Trim();
    }

    private sealed class TweetFactsResponse
    {
        public List<TweetItem> Tweets { get; set; } = [];
    }

    private sealed class TweetItem
    {
        public string Text { get; set; } = string.Empty;
        public List<string> Hashtags { get; set; } = [];
    }
}
