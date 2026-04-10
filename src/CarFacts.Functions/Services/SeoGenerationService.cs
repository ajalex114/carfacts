using System.Text;
using System.Text.Json;
using CarFacts.Functions.Models;
using CarFacts.Functions.Prompts;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;

namespace CarFacts.Functions.Services;

/// <summary>
/// Generates SEO metadata from a separate LLM pass that has full visibility
/// of all car facts content — produces stronger, more relevant SEO.
/// </summary>
public sealed class SeoGenerationService : ISeoGenerationService
{
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<SeoGenerationService> _logger;

    public SeoGenerationService(
        IChatCompletionService chatService,
        ILogger<SeoGenerationService> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public async Task<SeoMetadata> GenerateSeoAsync(RawCarFactsContent content, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating SEO metadata for {Count} facts", content.Facts.Count);

        var contentSummary = BuildContentSummary(content);
        var chatHistory = BuildChatHistory(contentSummary);
        var response = await _chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);

        var responseContent = response.Content
            ?? throw new InvalidOperationException("Empty AI response for SEO generation");

        return ParseResponse(responseContent);
    }

    private static string BuildContentSummary(RawCarFactsContent content)
    {
        var sb = new StringBuilder();
        foreach (var (fact, index) in content.Facts.Select((f, i) => (f, i)))
        {
            sb.AppendLine($"Fact {index + 1}: {fact.CatchyTitle}");
            sb.AppendLine($"Year: {fact.Year} | Car: {fact.CarModel}");
            sb.AppendLine(fact.Fact);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static ChatHistory BuildChatHistory(string contentSummary)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(PromptLoader.LoadSeoSystemPrompt());
        history.AddUserMessage(PromptLoader.LoadSeoUserPrompt(contentSummary));
        return history;
    }

    private SeoMetadata ParseResponse(string content)
    {
        var cleaned = CleanJsonResponse(content);
        var result = JsonSerializer.Deserialize<SeoMetadata>(cleaned)
            ?? throw new InvalidOperationException("Failed to deserialize SEO response");

        ValidateResponse(result);
        _logger.LogInformation("Generated SEO: {Title}", result.MainTitle);

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

    private static void ValidateResponse(SeoMetadata seo)
    {
        if (string.IsNullOrWhiteSpace(seo.MainTitle))
            throw new InvalidOperationException("SEO response missing main_title");
        if (string.IsNullOrWhiteSpace(seo.MetaDescription))
            throw new InvalidOperationException("SEO response missing meta_description");
        if (seo.Keywords.Count == 0)
            throw new InvalidOperationException("SEO response missing keywords");
    }
}
