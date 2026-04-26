using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace CarFacts.VideoFunction.Services;

/// <summary>
/// Uploads the rendered MP4 to Azure Blob Storage and returns a 48-hour SAS URL.
/// </summary>
public class VideoStorageService(string connectionString, string containerName)
{
    private readonly BlobContainerClient _container =
        new(connectionString, containerName);

    public async Task<string> UploadAsync(string filePath, string blobName)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.None);

        var blob = _container.GetBlobClient(blobName);

        await using var stream = File.OpenRead(filePath);
        await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = "video/mp4" });

        // Generate SAS URL valid for 48 hours
        var sasUri = blob.GenerateSasUri(
            Azure.Storage.Sas.BlobSasPermissions.Read,
            DateTimeOffset.UtcNow.AddHours(48));

        return sasUri.ToString();
    }
}
