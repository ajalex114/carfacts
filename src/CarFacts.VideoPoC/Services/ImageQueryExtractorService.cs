using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CarFacts.VideoPoC.Services;

/// <summary>
/// Extracts a clean Bing image search query from a car fact text.
/// Primary: Azure OpenAI chat completions (if configured).
/// Fallback: Regex brand/model/year extraction → "Ford Model T 1908".
/// </summary>
public class ImageQueryExtractorService(
    string? openAiEndpoint,
    string? openAiApiKey,
    string? deploymentName = null)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<string> ExtractQueryAsync(string factText)
    {
        if (!string.IsNullOrWhiteSpace(openAiEndpoint) && !string.IsNullOrWhiteSpace(openAiApiKey))
        {
            try
            {
                var result = await CallOpenAiAsync(factText);
                if (!string.IsNullOrWhiteSpace(result))
                    return result.Trim();
            }
            catch { /* fall through to regex */ }
        }
        return ExtractByRegex(factText);
    }

    private async Task<string?> CallOpenAiAsync(string factText)
    {
        var model    = string.IsNullOrWhiteSpace(deploymentName) ? "gpt-35-turbo" : deploymentName;
        var endpoint = openAiEndpoint!.TrimEnd('/');
        var url      = $"{endpoint}/openai/deployments/{model}/chat/completions?api-version=2024-02-01";

        var body = JsonSerializer.Serialize(new
        {
            messages = new[]
            {
                new { role = "system", content = "You extract car identifiers for image search. Reply with ONLY the brand, model, and year — nothing else. Example: Ford Model T 1908" },
                new { role = "user",   content = factText }
            },
            max_tokens  = 20,
            temperature = 0
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

    private static string ExtractByRegex(string factText)
    {
        var brand = SegmentPlanner.DetectBrand(factText);
        var year  = Regex.Match(factText, @"\b(1[89]\d{2}|20[012]\d)\b").Value;

        var parts = new List<string>();
        parts.Add(brand ?? "car");
        if (!string.IsNullOrEmpty(year)) parts.Add(year);

        return string.Join(" ", parts);
    }
}
