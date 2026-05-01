using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Uploads all post images to Azure Blob Storage and returns Blob URLs.
/// This runs BEFORE WordPress image upload so that Blob URLs are embedded in post HTML.
/// </summary>
public sealed class UploadImagesToBlobActivity
{
    private readonly IBlobImageStore _blobStore;
    private readonly ILogger<UploadImagesToBlobActivity> _logger;

    public UploadImagesToBlobActivity(
        IBlobImageStore blobStore,
        ILogger<UploadImagesToBlobActivity> logger)
    {
        _blobStore = blobStore;
        _logger = logger;
    }

    [Function(nameof(UploadImagesToBlobActivity))]
    public async Task<List<BlobUploadResult>> Run(
        [ActivityTrigger] UploadImagesToBlobInput input)
    {
        _logger.LogInformation("Uploading {Count} images to Blob Storage at prefix '{Prefix}'",
            input.Images.Count, input.PathPrefix);

        var results = new List<BlobUploadResult>();

        foreach (var image in input.Images)
        {
            var fact = input.Facts.Count > image.FactIndex ? input.Facts[image.FactIndex] : null;
            var altText = fact != null
                ? $"{fact.CarModel} ({fact.Year}) — {fact.CatchyTitle}"
                : $"Car image {image.FactIndex + 1}";

            var result = await _blobStore.UploadImageAsync(image, input.PathPrefix, altText);
            results.Add(result);
        }

        _logger.LogInformation("Uploaded {Count} images to Blob Storage", results.Count);
        return results;
    }
}
