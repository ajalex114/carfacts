using CarFacts.VideoPoC.Models;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CarFacts.VideoPoC.Services;

/// <summary>
/// Downloads and trims video clips from the Pexels API.
/// Caches downloaded source files by query so re-runs are instant.
/// </summary>
public class PexelsVideoService(string apiKey, string ffmpegPath, string cacheDir)
{
    private static readonly HttpClient Http = new();

    /// <summary>
    /// For each segment: search Pexels, download a matching clip, trim it to
    /// the segment duration, and return the updated segment with ClipPath set.
    /// </summary>
    public async Task<List<VideoSegment>> ResolveClipsAsync(
        List<VideoSegment> segments,
        string outputDir)
    {
        Directory.CreateDirectory(cacheDir);
        var resolved = new List<VideoSegment>();

        foreach (var seg in segments)
        {
            Console.Write($"  🎥  [{seg.SearchQuery}] ({seg.Duration:F1}s)... ");
            try
            {
                var sourcePath = await GetCachedSourceAsync(seg.SearchQuery);
                var clipPath   = Path.Combine(outputDir, $"clip_{resolved.Count:D2}.mp4");
                await TrimClipAsync(sourcePath, clipPath, seg.Duration);
                resolved.Add(seg with { ClipPath = clipPath });
                Console.WriteLine("done");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"failed ({ex.Message}) — will use image fallback");
                resolved.Add(seg with { ClipPath = null });
            }
        }

        return resolved;
    }

    // ── Pexels search + download ─────────────────────────────────────────────

    private async Task<string> GetCachedSourceAsync(string query)
    {
        var safeKey  = string.Concat(query.Split(Path.GetInvalidFileNameChars()))
                            .Replace(' ', '_').ToLowerInvariant();
        var cachePath = Path.Combine(cacheDir, $"{safeKey}.mp4");

        if (File.Exists(cachePath))
            return cachePath;

        var videoUrl = await SearchPexelsAsync(query);
        await DownloadFileAsync(videoUrl, cachePath);
        return cachePath;
    }

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

        // Pick best quality HD file
        var file = video.VideoFiles?
            .Where(f => f.FileType == "video/mp4")
            .OrderByDescending(f => f.Width * f.Height)
            .FirstOrDefault()
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
            "-vf", "scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,setsar=1",
            "-r", "30",
            "-c:v", "libx264", "-preset", "fast", "-crf", "22",
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
