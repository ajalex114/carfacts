using CarFacts.Functions.Models;

namespace CarFacts.Functions.Services.Interfaces;

public interface IWordPressService
{
    Task<List<UploadedMedia>> UploadImagesAsync(List<GeneratedImage> images, List<CarFact> facts, CancellationToken cancellationToken = default);
    Task<WordPressPostResult> CreatePostAsync(string title, string htmlContent, string excerpt, int featuredMediaId, string seoKeywords, string metaDescription, CancellationToken cancellationToken = default);
}
