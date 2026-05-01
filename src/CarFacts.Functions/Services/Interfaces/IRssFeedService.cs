namespace CarFacts.Functions.Services.Interfaces;

/// <summary>
/// Generates an RSS 2.0 feed from recent posts and writes it to Azure Blob Storage.
/// </summary>
public interface IRssFeedService
{
    /// <summary>
    /// Regenerates the RSS feed XML from the most recent posts.
    /// Writes to Blob Storage at "feed/rss.xml".
    /// </summary>
    Task RegenerateFeedAsync(CancellationToken cancellationToken = default);
}
