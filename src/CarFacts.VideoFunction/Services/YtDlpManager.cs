using Azure.Storage.Blobs;

namespace CarFacts.VideoFunction.Services;

/// <summary>
/// Ensures yt-dlp.exe (and optionally youtube-cookies.txt) are available in Temp.
/// Same pattern as FfmpegManager — downloaded from poc-tools blob on cold start,
/// cached in static fields so warm invocations skip the download.
/// yt-dlp must run from Temp (not Azure Files) for the same reason as FFmpeg.
///
/// Cookies: upload youtube-cookies.txt to poc-tools blob to bypass YouTube bot detection
/// on Azure datacenter IPs. If the blob does not exist, yt-dlp runs without auth (will
/// likely fail on bot-protected videos and fall back to Pexels).
/// </summary>
public class YtDlpManager(string storageConnectionString,
                          string toolsContainer = "poc-tools",
                          string blobName       = "yt-dlp.exe")
{
    private static readonly SemaphoreSlim Lock        = new(1, 1);
    private static readonly SemaphoreSlim CookiesLock = new(1, 1);
    private static string? _cachedPath;
    private static string? _cachedCookiesPath; // "" = checked and not found

    public async Task<string> EnsureReadyAsync()
    {
        if (_cachedPath is not null && File.Exists(_cachedPath))
            return _cachedPath;

        await Lock.WaitAsync();
        try
        {
            if (_cachedPath is not null && File.Exists(_cachedPath))
                return _cachedPath;

            var binDir  = Path.Combine(Path.GetTempPath(), "poc-ytdlp-bin");
            var exePath = Path.Combine(binDir, "yt-dlp.exe");

            if (!File.Exists(exePath))
            {
                Directory.CreateDirectory(binDir);
                Console.WriteLine($"⬇️  Downloading yt-dlp.exe from Azure Blob to {binDir}...");

                var blob = new BlobClient(storageConnectionString, toolsContainer, blobName);
                await blob.DownloadToAsync(exePath);

                Console.WriteLine($"   Saved ({new FileInfo(exePath).Length / 1024} KB)");
            }
            else
            {
                Console.WriteLine($"   yt-dlp.exe already cached at {exePath}");
            }

            _cachedPath = exePath;
            return exePath;
        }
        finally
        {
            Lock.Release();
        }
    }

    /// <summary>
    /// Tries to download youtube-cookies.txt from poc-tools blob.
    /// Returns the local path if found, null if the blob doesn't exist.
    /// Cookies allow yt-dlp to bypass YouTube's bot detection on datacenter IPs.
    /// To enable: export cookies from a logged-in browser and upload as
    /// "youtube-cookies.txt" to the poc-tools blob container.
    /// </summary>
    public async Task<string?> EnsureCookiesAsync()
    {
        if (_cachedCookiesPath is not null)
            return _cachedCookiesPath.Length > 0 && File.Exists(_cachedCookiesPath)
                ? _cachedCookiesPath : null;

        await CookiesLock.WaitAsync();
        try
        {
            if (_cachedCookiesPath is not null)
                return _cachedCookiesPath.Length > 0 && File.Exists(_cachedCookiesPath)
                    ? _cachedCookiesPath : null;

            var binDir      = Path.Combine(Path.GetTempPath(), "poc-ytdlp-bin");
            var cookiesPath = Path.Combine(binDir, "youtube-cookies.txt");

            if (!File.Exists(cookiesPath))
            {
                Directory.CreateDirectory(binDir);
                var blob = new BlobClient(storageConnectionString, toolsContainer, "youtube-cookies.txt");
                if (await blob.ExistsAsync())
                {
                    await blob.DownloadToAsync(cookiesPath);
                    Console.WriteLine("   🍪 youtube-cookies.txt downloaded — yt-dlp will use auth cookies");
                    _cachedCookiesPath = cookiesPath;
                }
                else
                {
                    Console.WriteLine("   ℹ️  No youtube-cookies.txt in poc-tools — yt-dlp will run without auth");
                    _cachedCookiesPath = ""; // sentinel: checked, not found
                }
            }
            else
            {
                _cachedCookiesPath = cookiesPath;
            }

            return string.IsNullOrEmpty(_cachedCookiesPath) ? null : _cachedCookiesPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Could not check cookies blob: {ex.Message}");
            _cachedCookiesPath = "";
            return null;
        }
        finally
        {
            CookiesLock.Release();
        }
    }
}
