namespace CarFacts.VideoFunction.Services;

/// <summary>Holds the Pexels API key — injected via DI.</summary>
public class PexelsApiKeyHolder(string apiKey)
{
    public string ApiKey { get; } = apiKey;
}
