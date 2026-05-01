using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CarFacts.Functions.Configuration;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace CarFacts.Functions.Services;

public sealed class BlobImageUploadService : IBlobImageStore
{
    private readonly BlobServiceClient _serviceClient;
    private readonly BlobStorageSettings _settings;
    private readonly ILogger<BlobImageUploadService> _logger;

    public BlobImageUploadService(
        IOptions<BlobStorageSettings> settings,
        ILogger<BlobImageUploadService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        // Use DefaultAzureCredential when running in Azure (account name configured);
        // fall back to connection string for local development.
        if (!string.IsNullOrEmpty(_settings.ConnectionString))
        {
            _serviceClient = new BlobServiceClient(_settings.ConnectionString);
        }
        else
        {
            var accountUri = new Uri($"https://{_settings.AccountName}.blob.core.windows.net");
            _serviceClient = new BlobServiceClient(accountUri, new DefaultAzureCredential());
        }
    }

    public async Task<BlobUploadResult> UploadImageAsync(
        GeneratedImage image,
        string pathPrefix,
        string altText,
        CancellationToken cancellationToken = default)
    {
        var blobPath = $"{pathPrefix}{image.FactIndex}.png";
        var containerClient = _serviceClient.GetBlobContainerClient(_settings.ImagesContainerName);

        // Ensure container exists with public blob access
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(blobPath);

        using var stream = new MemoryStream(image.ImageData);

        await blobClient.UploadAsync(stream, new BlobHttpHeaders
        {
            ContentType = "image/png",
            CacheControl = "public, max-age=31536000, immutable"
        }, cancellationToken: cancellationToken);

        var blobUrl = blobClient.Uri.ToString();

        _logger.LogInformation("Uploaded image to Blob: {Path} → {Url}", blobPath, blobUrl);

        return new BlobUploadResult
        {
            FactIndex = image.FactIndex,
            BlobUrl = blobUrl,
            BlobPath = blobPath,
            AltText = altText
        };
    }

    public async Task UploadTextFileAsync(
        string content,
        string blobPath,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _serviceClient.GetBlobContainerClient(_settings.WebFeedsContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(blobPath);

        var bytes = Encoding.UTF8.GetBytes(content);
        using var stream = new MemoryStream(bytes);

        await blobClient.UploadAsync(stream, new BlobHttpHeaders
        {
            ContentType = contentType,
            CacheControl = "public, max-age=300"  // 5-minute TTL for dynamic XML files
        }, cancellationToken: cancellationToken);

        _logger.LogInformation("Uploaded text file to Blob: {Path} ({ContentType})", blobPath, contentType);
    }
}
