namespace CarFacts.Functions.Services.Interfaces;

public interface ISocialMediaService
{
    string PlatformName { get; }
    bool IsEnabled { get; }
    Task PostAsync(string teaser, string postUrl, string postTitle, List<string> keywords, CancellationToken cancellationToken = default);

    /// <summary>Posts pre-formatted content directly (no assembly needed).</summary>
    Task PostRawAsync(string content, CancellationToken cancellationToken = default);
}
