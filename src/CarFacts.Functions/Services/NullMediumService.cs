using CarFacts.Functions.Services.Interfaces;

namespace CarFacts.Functions.Services;

public sealed class NullMediumService : IMediumService
{
    public bool IsEnabled => false;

    public Task<MediumPublishResult> PublishArticleAsync(
        string title, string htmlContent, string canonicalUrl,
        List<string> tags, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MediumPublishResult { Success = false });
    }
}
