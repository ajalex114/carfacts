using System.Text;
using System.Text.Json;

namespace CarFacts.VideoFunction.Services;

/// <summary>
/// Generates a single ~50-word car fact for short-form video narration.
/// Uses Azure OpenAI with the CarFactVideoPrompt.txt prompt (tunable independently
/// of the blog pipeline). Throws if OpenAI is not configured or the call fails —
/// there is no hardcoded fallback; every video must be LLM-generated.
/// </summary>
public class CarFactGenerationService(
    string? openAiEndpoint,
    string? openAiApiKey,
    string? deploymentName = null)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private static readonly Lazy<string> Prompt = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Prompts", "CarFactVideoPrompt.txt");
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    });

    public async Task<string> GenerateFactAsync()
    {
        if (string.IsNullOrWhiteSpace(openAiEndpoint) || string.IsNullOrWhiteSpace(openAiApiKey))
            throw new InvalidOperationException(
                "OpenAI:Endpoint and OpenAI:ApiKey must be configured. No hardcoded fallback — all facts are LLM-generated.");

        var result = await CallOpenAiAsync();
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException("OpenAI returned an empty response for car fact generation.");

        return result.Trim();
    }

    private async Task<string?> CallOpenAiAsync()
    {
        var model    = string.IsNullOrWhiteSpace(deploymentName) ? "gpt-35-turbo" : deploymentName;
        var endpoint = openAiEndpoint!.TrimEnd('/');
        var url      = $"{endpoint}/openai/deployments/{model}/chat/completions?api-version=2024-02-01";
        var prompt   = Prompt.Value;

        if (string.IsNullOrWhiteSpace(prompt))
            return null;

        var body = JsonSerializer.Serialize(new
        {
            messages = new[]
            {
                new { role = "system", content = prompt },
                // Unique token per call prevents any API-side caching from returning identical results.
                new { role = "user",   content = $"Generate a car fact now. [uid:{Guid.NewGuid():N},{DateTime.UtcNow:HHmmss}]" }
            },
            max_tokens  = 150,
            temperature = 0.9   // high variety — each of the 5 daily runs gets a different fact
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("api-key", openAiApiKey);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }
}
