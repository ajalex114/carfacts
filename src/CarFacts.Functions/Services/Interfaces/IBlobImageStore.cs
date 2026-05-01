using CarFacts.Functions.Models;

namespace CarFacts.Functions.Services.Interfaces;

/// <summary>
/// Uploads post images to Azure Blob Storage.
/// Returns Blob URLs that are embedded in post HTML (replacing WordPress CDN URLs).
/// </summary>
public interface IBlobImageStore
{
    /// <summary>
    /// Uploads a single generated image to Blob Storage.
    /// </summary>
    /// <param name="image">The generated image data.</param>
    /// <param name="pathPrefix">Blob path prefix, e.g. "2026/04/15/my-post-slug/"</param>
    /// <param name="altText">Alt text for the image.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<BlobUploadResult> UploadImageAsync(
        GeneratedImage image,
        string pathPrefix,
        string altText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a text file (XML, RSS, etc.) to the web-feeds Blob container.
    /// </summary>
    Task UploadTextFileAsync(
        string content,
        string blobPath,
        string contentType,
        CancellationToken cancellationToken = default);
}
