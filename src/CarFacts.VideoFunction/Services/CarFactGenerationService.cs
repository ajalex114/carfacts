using CarFacts.VideoFunction.Models;
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

    // Full pool of premium brands — luxury, sports, supercar, hypercar, muscle.
    // No mainstream or economy cars.
    public static readonly string[] AllBrands =
    [
        "Ferrari", "Lamborghini", "Porsche", "McLaren", "Bugatti", "Koenigsegg",
        "Pagani", "Rimac", "Pininfarina", "Czinger", "SSC", "Hennessey",
        "Aston Martin", "Rolls-Royce", "Bentley", "Maybach", "Mercedes-AMG", "BMW M",
        "Lotus", "Maserati", "Alfa Romeo", "Nissan GT-R", "Ford GT",
        "Lexus LFA", "Cadillac CT5-V Blackwing", "Chevrolet Corvette Z06",
        "Dodge Viper", "Dodge Challenger Hellcat", "Ford Mustang Shelby GT500",
        "Lucid Air Sapphire", "Tesla Model S Plaid"
    ];

    public Task<string> GenerateFactAsync() => GenerateFactAsync([], 15, 18);
    public Task<string> GenerateFactAsync(IEnumerable<string> excludedBrands) => GenerateFactAsync(excludedBrands, 15, 18);

    public async Task<string> GenerateFactAsync(IEnumerable<string> excludedBrands, int targetSecMin, int targetSecMax)
    {
        if (string.IsNullOrWhiteSpace(openAiEndpoint) || string.IsNullOrWhiteSpace(openAiApiKey))
            throw new InvalidOperationException(
                "OpenAI:Endpoint and OpenAI:ApiKey must be configured. No hardcoded fallback — all facts are LLM-generated.");

        var result = await CallOpenAiAsync(null, excludedBrands, targetSecMin, targetSecMax);
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException("OpenAI returned an empty response for car fact generation.");

        return result.Trim();
    }

    public async Task<string> GenerateFactAsync(BrandModelSelection selection, int targetSecMin, int targetSecMax, string narrationStyle = "")
    {
        if (string.IsNullOrWhiteSpace(openAiEndpoint) || string.IsNullOrWhiteSpace(openAiApiKey))
            throw new InvalidOperationException(
                "OpenAI:Endpoint and OpenAI:ApiKey must be configured. No hardcoded fallback — all facts are LLM-generated.");

        var result = await CallOpenAiAsync(selection, [], targetSecMin, targetSecMax, narrationStyle);
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException("OpenAI returned an empty response for car fact generation.");

        return result.Trim();
    }

    public async Task<string> GenerateFactAsync(BrandModelSelection selection, int targetSecMin, int targetSecMax, string narrationStyle, Models.NewsItem? newsContext)
    {
        if (string.IsNullOrWhiteSpace(openAiEndpoint) || string.IsNullOrWhiteSpace(openAiApiKey))
            throw new InvalidOperationException(
                "OpenAI:Endpoint and OpenAI:ApiKey must be configured. No hardcoded fallback — all facts are LLM-generated.");

        var result = await CallOpenAiAsync(selection, [], targetSecMin, targetSecMax, narrationStyle, newsContext);
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException("OpenAI returned an empty response for car fact generation.");

        return result.Trim();
    }

    private async Task<string?> CallOpenAiAsync(BrandModelSelection? selection, IEnumerable<string> excludedBrands, int targetSecMin = 15, int targetSecMax = 18, string narrationStyle = "", Models.NewsItem? newsContext = null)
    {
        var model    = string.IsNullOrWhiteSpace(deploymentName) ? "gpt-35-turbo" : deploymentName;
        var endpoint = openAiEndpoint!.TrimEnd('/');
        var url      = $"{endpoint}/openai/deployments/{model}/chat/completions?api-version=2024-02-01";
        var prompt   = Prompt.Value;

        if (string.IsNullOrWhiteSpace(prompt))
            return null;

        // TTS prosody rate is 0.88 → effective pace ≈ 2.2 words/sec (not 2.5)
        var targetWords = (int)Math.Round((targetSecMin + targetSecMax) / 2.0 * 2.2);

        // Resolve narration style instruction (if provided)
        var styleInstruction = "";
        if (!string.IsNullOrWhiteSpace(narrationStyle))
        {
            var style = NarrationStyles.All.FirstOrDefault(s => s.Name == narrationStyle);
            if (style != null)
                styleInstruction = $" Narration style: {style.Instruction}";
        }

        string userMessage;
        var todayContext = $"Today is {DateTime.UtcNow:MMMM d, yyyy}.";
        var newsPrefix   = newsContext != null
            ? $"RECENT NEWS about this brand (published {newsContext.PublishedAt:MMMM d, yyyy}): \"{newsContext.Title}\". {newsContext.Summary} — Ground the car fact in this real news event if relevant. "
            : string.Empty;
        if (selection != null)
        {
            var modelPart = !string.IsNullOrWhiteSpace(selection.Model)
                ? $" {selection.Model}"
                : string.Empty;
            userMessage = $"{todayContext} {newsPrefix}Generate a car fact now about the {selection.Brand}{modelPart}. " +
                          $"STRICT LIMIT: {targetWords} words maximum (~{targetSecMin}-{targetSecMax} seconds spoken). Do not exceed {targetWords} words." +
                          $"{styleInstruction} " +
                          $"[sel:{selection.Reason},uid:{Guid.NewGuid():N},{DateTime.UtcNow:HHmmss}]";
        }
        else
        {
            var excluded = excludedBrands
                .Select(b => b.ToLowerInvariant())
                .ToHashSet();
            var available = AllBrands
                .Where(b => !excluded.Contains(b.ToLowerInvariant()))
                .ToArray();
            if (available.Length == 0) available = AllBrands;

            var brand = available[Random.Shared.Next(available.Length)];
            var excludedHint = excluded.Count > 0
                ? $" Avoid these brands that were recently used: {string.Join(", ", excluded)}."
                : string.Empty;
            userMessage = $"{todayContext} {newsPrefix}Generate a car fact now about a {brand} model. " +
                          $"STRICT LIMIT: {targetWords} words maximum (~{targetSecMin}-{targetSecMax} seconds spoken). Do not exceed {targetWords} words." +
                          $"{excludedHint}{styleInstruction} [uid:{Guid.NewGuid():N},{DateTime.UtcNow:HHmmss}]";
        }

        var body = JsonSerializer.Serialize(new
        {
            messages = new[]
            {
                new { role = "system", content = prompt },
                new { role = "user",   content = userMessage }
            },
            max_tokens  = targetWords * 2, // ~2 tokens/word — strict cap prevents over-generation
            temperature = 0.9
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
