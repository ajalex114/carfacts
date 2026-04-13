namespace CarFacts.Functions.Services.Interfaces;

public interface IMediumService
{
    bool IsEnabled { get; }

    /// <summary>
    /// Publishes an article to Medium with a canonical link back to the original post.
    /// </summary>
    Task<MediumPublishResult> PublishArticleAsync(
        string title,
        string htmlContent,
        string canonicalUrl,
        List<string> tags,
        CancellationToken cancellationToken = default);
}

public sealed class MediumPublishResult
{
    public bool Success { get; set; }
    public string MediumUrl { get; set; } = string.Empty;
    public string MediumPostId { get; set; } = string.Empty;
}
