using System.Text.Json;
using CarFacts.Functions.Models;
using CarFacts.Functions.Prompts;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace CarFacts.Functions.Services;

public sealed class ContentGenerationService : IContentGenerationService
{
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<ContentGenerationService> _logger;

    public ContentGenerationService(
        IChatCompletionService chatService,
        ILogger<ContentGenerationService> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public async Task<CarFactsResponse> GenerateFactsAsync(string todayDate, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating car facts for {Date} via Semantic Kernel", todayDate);

        var chatHistory = BuildChatHistory(todayDate);
        var response = await _chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);

        var content = response.Content
            ?? throw new InvalidOperationException("Empty AI response");

        return ParseResponse(content);
    }

    private static ChatHistory BuildChatHistory(string todayDate)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(PromptLoader.LoadSystemPrompt());
        history.AddUserMessage(PromptLoader.LoadUserPrompt(todayDate));
        return history;
    }

    private CarFactsResponse ParseResponse(string content)
    {
        var cleaned = CleanJsonResponse(content);
        var result = JsonSerializer.Deserialize<CarFactsResponse>(cleaned)
            ?? throw new InvalidOperationException("Failed to deserialize AI response");

        ValidateResponse(result);
        _logger.LogInformation("Generated {FactCount} facts with title: {Title}", result.Facts.Count, result.MainTitle);

        return result;
    }

    private static string CleanJsonResponse(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```"))
            return trimmed;

        return trimmed
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();
    }

    private static void ValidateResponse(CarFactsResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.MainTitle))
            throw new InvalidOperationException("AI response missing main_title");

        if (response.Facts.Count != 5)
            throw new InvalidOperationException($"Expected 5 facts, got {response.Facts.Count}");

        for (int i = 0; i < response.Facts.Count; i++)
        {
            var fact = response.Facts[i];
            if (string.IsNullOrWhiteSpace(fact.Fact) || string.IsNullOrWhiteSpace(fact.CarModel))
                throw new InvalidOperationException($"Fact {i + 1} is missing required fields");
        }
    }
}
