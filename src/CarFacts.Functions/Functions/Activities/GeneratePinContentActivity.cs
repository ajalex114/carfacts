using System.Text.Json;
using CarFacts.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Generates a Pinterest pin title and description via LLM for a given car fact.
/// Falls back to a template-based approach if LLM fails.
/// </summary>
public sealed class GeneratePinContentActivity
{
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<GeneratePinContentActivity> _logger;

    public GeneratePinContentActivity(
        IChatCompletionService chatService,
        ILogger<GeneratePinContentActivity> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [Function(nameof(GeneratePinContentActivity))]
    public async Task<PinContent> Run(
        [ActivityTrigger] GeneratePinContentInput input)
    {
        try
        {
            var systemPrompt = await LoadPromptAsync("PinterestPinSystemPrompt.txt");
            var userPrompt = (await LoadPromptAsync("PinterestPinUserPrompt.txt"))
                .Replace("{car_model}", input.CarModel)
                .Replace("{year}", input.Year.ToString())
                .Replace("{title}", input.Title)
                .Replace("{keywords}", string.Join(", ", input.Keywords));

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userPrompt);

            var result = await _chatService.GetChatMessageContentAsync(chatHistory);
            var content = result.Content?.Trim() ?? "";

            // Strip markdown code fences if present
            if (content.StartsWith("```"))
            {
                content = content.Split('\n', 2).Length > 1 ? content.Split('\n', 2)[1] : content;
                if (content.EndsWith("```"))
                    content = content[..^3].Trim();
            }

            var parsed = JsonSerializer.Deserialize<PinContent>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed != null && !string.IsNullOrEmpty(parsed.Title))
            {
                _logger.LogInformation("Generated pin content: '{Title}'", parsed.Title);
                return parsed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM pin content generation failed — using fallback template");
        }

        // Fallback template
        return new PinContent
        {
            Title = $"{input.CarModel} ({input.Year}) — Did You Know?",
            Description = $"Discover an amazing fact about the {input.Year} {input.CarModel}. " +
                $"Click through to read the full story and more fascinating car facts."
        };
    }

    private static async Task<string> LoadPromptAsync(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", fileName);
        return await File.ReadAllTextAsync(path);
    }
}
