using CarFacts.Functions.Models;

namespace CarFacts.Functions.Services.Interfaces;

public interface IWordPressService
{
    Task<List<UploadedMedia>> UploadImagesAsync(List<GeneratedImage> images, List<CarFact> facts, CancellationToken cancellationToken = default);
    Task<UploadedMedia> UploadSingleImageAsync(GeneratedImage image, CarFact fact, int parentPostId, CancellationToken cancellationToken = default);
    Task<WordPressPostResult> CreatePostAsync(string title, string htmlContent, string excerpt, int featuredMediaId, string seoKeywords, string metaDescription, CancellationToken cancellationToken = default);
    Task<WordPressPostResult> CreateDraftPostAsync(string title, CancellationToken cancellationToken = default);
    Task<WordPressPostResult> UpdateAndPublishPostAsync(int postId, string title, string htmlContent, string excerpt, int featuredMediaId, string seoKeywords, string metaDescription, CancellationToken cancellationToken = default);
    Task AssociateMediaWithPostAsync(List<UploadedMedia> media, int postId, CancellationToken cancellationToken = default);
    Task<WordPressPostResult> CreateWebStoryAsync(string title, string content, string excerpt, CancellationToken cancellationToken = default);
}
