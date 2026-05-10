using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Uploads a single post image to Azure Blob Storage and returns the Blob URL.
/// Called in parallel from the orchestrator (fan-out pattern).
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
    public async Task<BlobUploadResult> Run(
        [ActivityTrigger] UploadSingleImageToBlobInput input)
    {
        _logger.LogInformation("Uploading image {Index} to Blob Storage at '{Path}'",
            input.Image.FactIndex, input.PathPrefix);

        var result = await _blobStore.UploadImageAsync(input.Image, input.PathPrefix, input.AltText);

        _logger.LogInformation("Uploaded image {Index} to {Url}", input.Image.FactIndex, result.BlobUrl);
        return result;
    }
}
