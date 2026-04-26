using CarFacts.VideoFunction.Models;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CarFacts.VideoFunction.Services;

/// <summary>
/// Downloads and trims video clips from the Pexels API.
/// Source videos are downloaded to a short-lived temp file and deleted after trimming.
/// Trimmed clips are cached in Temp keyed by query+duration so warm re-runs are instant.
/// Downloads are throttled to 2 concurrent to stay within the 500 MB temp disk budget.
/// </summary>
public class PexelsVideoService(string apiKey, string ffmpegPath, string cacheDir)
{
    private static readonly HttpClient Http = new();
    private static readonly SemaphoreSlim DownloadSem = new(2); // max 2 concurrent downloads

    /// <summary>
    /// For each segment: search Pexels, download a matching clip, trim it to
    /// the segment duration. Max 2 downloads run simultaneously to control disk usage.
    /// </summary>
    public async Task<List<VideoSegment>> ResolveClipsAsync(
        List<VideoSegment> segments,
        string outputDir)
    {
        Directory.CreateDirectory(cacheDir);

        // Throttled parallel downloads — limit to 2 concurrent to stay under 500 MB temp disk
        var downloadTasks = segments.Select((seg, i) => ResolveOneAsync(seg, i, outputDir));
        var results = await Task.WhenAll(downloadTasks);

        return [.. results];
    }

    private async Task<VideoSegment> ResolveOneAsync(VideoSegment seg, int index, string outputDir)
    {
        var clipPath = Path.Combine(outputDir, $"clip_{index:D2}.mp4");

        // Check if this trimmed clip is already cached (keyed by query + duration)
        var safeKey    = string.Concat(seg.SearchQuery.Split(Path.GetInvalidFileNameChars()))
                              .Replace(' ', '_').ToLowerInvariant();
        var durKey     = ((int)(seg.Duration * 10)).ToString();
        var cachedClip = Path.Combine(cacheDir, $"{safeKey}_{durKey}.mp4");

        if (File.Exists(cachedClip))
        {
            File.Copy(cachedClip, clipPath, overwrite: true);
            return seg with { ClipPath = clipPath };
        }

        // Throttle: max 2 concurrent downloads to stay within 500 MB temp disk budget
        await DownloadSem.WaitAsync();
        var sourceTmp = Path.Combine(Path.GetTempPath(), $"src_{Guid.NewGuid():N}.mp4");
        try
        {
            var videoUrl = await SearchPexelsAsync(seg.SearchQuery);
            await DownloadFileAsync(videoUrl, sourceTmp);
            await TrimClipAsync(sourceTmp, clipPath, seg.Duration);

            // Cache the trimmed clip for future warm runs
            Directory.CreateDirectory(cacheDir);
            File.Copy(clipPath, cachedClip, overwrite: true);

            return seg with { ClipPath = clipPath };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  [{seg.SearchQuery}] failed: {ex.Message}");
            return seg with { ClipPath = null };
        }
        finally
        {
            try { File.Delete(sourceTmp); } catch { /* best effort */ }
            DownloadSem.Release();
        }
    }

    // ── Pexels search + download ─────────────────────────────────────────────

    private async Task<string> SearchPexelsAsync(string query)
    {
        var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.pexels.com/videos/search?query={Uri.EscapeDataString(query)}&per_page=10&orientation=portrait");
        req.Headers.Authorization = new AuthenticationHeaderValue(apiKey);

        var resp = await Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<PexelsSearchResult>()
            ?? throw new InvalidOperationException("Empty Pexels response");

        // Try portrait first; fall back to any orientation
        var video = result.Videos?.FirstOrDefault(v => v.Height > v.Width)
                 ?? result.Videos?.FirstOrDefault()
                 ?? throw new InvalidOperationException($"No Pexels results for '{query}'");

        // Pick the smallest file that is still ≥ 540px wide (good enough for 1080x1920 after scale)
        // Smaller file = faster download on cold start — FFmpeg will upscale during trim
        var file = video.VideoFiles?
            .Where(f => f.FileType == "video/mp4" && f.Width >= 540)
            .OrderBy(f => f.Width * f.Height)   // smallest adequate resolution
            .FirstOrDefault()
            // fallback: any MP4
            ?? video.VideoFiles?.Where(f => f.FileType == "video/mp4")
                   .OrderBy(f => f.Width * f.Height).FirstOrDefault()
            ?? throw new InvalidOperationException("No MP4 file found in Pexels result");

        return file.Link ?? throw new InvalidOperationException("Pexels file link is null");
    }

    private static async Task DownloadFileAsync(string url, string path)
    {
        using var resp   = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync();
        await using var file   = File.Create(path);
        await stream.CopyToAsync(file);
    }

    // ── FFmpeg trim ──────────────────────────────────────────────────────────

    private async Task TrimClipAsync(string sourcePath, string outputPath, double duration)
    {
        // Use -ss on input side for fast seek, -t for duration, re-encode to ensure clean trim
        var psi = new ProcessStartInfo
        {
            FileName             = ffmpegPath,
            UseShellExecute      = false,
            RedirectStandardError = true,
        };

        foreach (var arg in new[]
        {
            "-y",
            "-ss", "0",
            "-i", sourcePath,
            "-t", duration.ToString("F3"),
            "-vf", "scale=720:1280:force_original_aspect_ratio=increase,crop=720:1280,setsar=1",
            "-r", "30",
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "24",
            "-an",   // strip audio from clips — we overlay TTS separately
            outputPath
        }) psi.ArgumentList.Add(arg);

        using var proc = Process.Start(psi)!;
        await proc.StandardError.ReadToEndAsync(); // consume stderr to avoid deadlock
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg trim failed (exit {proc.ExitCode})");
    }

    // ── JSON models ──────────────────────────────────────────────────────────

    private record PexelsSearchResult(
        [property: JsonPropertyName("videos")] List<PexelsVideo>? Videos);

    private record PexelsVideo(
        [property: JsonPropertyName("id")]          int Id,
        [property: JsonPropertyName("width")]       int Width,
        [property: JsonPropertyName("height")]      int Height,
        [property: JsonPropertyName("video_files")] List<PexelsVideoFile>? VideoFiles);

    private record PexelsVideoFile(
        [property: JsonPropertyName("quality")]   string? Quality,
        [property: JsonPropertyName("file_type")] string? FileType,
        [property: JsonPropertyName("width")]     int Width,
        [property: JsonPropertyName("height")]    int Height,
        [property: JsonPropertyName("link")]      string? Link);
}

