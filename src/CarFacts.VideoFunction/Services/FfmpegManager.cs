using Azure.Storage.Blobs;

namespace CarFacts.VideoFunction.Services;

/// <summary>
/// Ensures an FFmpeg binary is available.
/// Downloaded to C:\local\Temp (required for execution — Azure Files blocks code execution).
/// Cached in a static field so warm invocations skip the download entirely.
/// Note: ffprobe is intentionally NOT downloaded — disk space on Consumption plan is ~500MB.
///       yt-dlp uses --no-check-formats to bypass ffprobe requirement.
/// </summary>
public class FfmpegManager(string storageConnectionString,
                           string toolsContainer = "poc-tools",
                           string blobName       = "ffmpeg.exe")
{
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static string? _cachedPath;

    public async Task<string> EnsureReadyAsync()
    {
        if (_cachedPath is not null && File.Exists(_cachedPath))
            return _cachedPath;

        await Lock.WaitAsync();
        try
        {
            if (_cachedPath is not null && File.Exists(_cachedPath))
                return _cachedPath;

            // Must use Temp (not C:\home / Azure Files) — Windows refuses to execute from network shares
            var binDir  = Path.Combine(Path.GetTempPath(), "poc-ffmpeg-bin");
            var exePath = Path.Combine(binDir, "ffmpeg.exe");

            if (!File.Exists(exePath))
            {
                Directory.CreateDirectory(binDir);
                Console.WriteLine($"⬇️  Downloading ffmpeg.exe from Azure Blob to {binDir}...");

                var blob = new BlobClient(storageConnectionString, toolsContainer, blobName);
                await blob.DownloadToAsync(exePath);

                Console.WriteLine($"   Saved ({new FileInfo(exePath).Length / 1024 / 1024} MB)");
            }
            else
            {
                Console.WriteLine($"   ffmpeg.exe already cached at {exePath}");
            }

            _cachedPath = exePath;
            return exePath;
        }
        finally
        {
            Lock.Release();
        }
    }
}

