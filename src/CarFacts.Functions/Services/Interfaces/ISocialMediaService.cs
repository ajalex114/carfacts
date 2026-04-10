namespace CarFacts.Functions.Services.Interfaces;

public interface ISocialMediaService
{
    string PlatformName { get; }
    bool IsEnabled { get; }
    Task PostAsync(string teaser, string postUrl, string postTitle, List<string> keywords, CancellationToken cancellationToken = default);
}
