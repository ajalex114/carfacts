using CarFacts.VideoPoC.Models;
using System.Diagnostics;

namespace CarFacts.VideoPoC.Services;

public class VideoGenerator(string ffmpegPath = "ffmpeg")
{
    private const double XfadeDuration = 0.3; // seconds between clips

    // ── Single-image mode (Ken Burns) ────────────────────────────────────────

    public async Task GenerateAsync(
        string imagePath,
        string audioPath,
        string subtitleFileName,   // just filename — ffmpeg runs in outputDir
        string? musicPath,
        string outputPath,
        double duration,
        int fps = 30)
    {
        var totalFrames = (int)Math.Ceiling(duration * fps) + 5;

        var kenBurns =
            "[0:v]" +
            "scale=1080:1920:force_original_aspect_ratio=increase," +
            "crop=1080:1920," +
            "scale=2160:3840," +
            $"zoompan=z='1+0.0003*on':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':" +
            $"d={totalFrames}:fps={fps}:s=1080x1920" +
            "[bg]";

        var subFilter = $"[bg]ass='{subtitleFileName}'[v]";
        var filterParts = new List<string> { kenBurns, subFilter };

        bool hasMusic = musicPath is not null;
        string audioMap;
        if (hasMusic)
        {
            filterParts.Add("[1:a]volume=1.0[narr]");
            filterParts.Add("[2:a]volume=0.12[music]");
            filterParts.Add("[narr][music]amix=inputs=2:duration=first:dropout_transition=2[audio]");
            audioMap = "[audio]";
        }
        else
        {
            audioMap = "1:a";
        }

        var psi = BuildPsi(outputPath);
        void Add(params string[] args) { foreach (var a in args) psi.ArgumentList.Add(a); }

        Add("-loop", "1", "-i", imagePath);
        Add("-i", audioPath);
        if (hasMusic) Add("-stream_loop", "-1", "-i", musicPath!);

        Add("-filter_complex", string.Join(";", filterParts));
        Add("-map", "[v]", "-map", audioMap);
        Add("-vcodec", "libx264", "-preset", "fast", "-crf", "22");
        Add("-acodec", "aac", "-ar", "44100", "-ac", "2");
        Add("-r", fps.ToString(), "-t", duration.ToString("F3"));
        Add("-movflags", "+faststart");
        Add("-y", outputPath);

        await RunAsync(psi);
    }

    // ── Multi-clip mode (dynamic video segments with xfade) ──────────────────

    /// <summary>
    /// Stitches pre-trimmed 1080×1920 clip files together with xfade transitions,
    /// overlays TTS audio and ASS subtitles, and writes the final MP4.
    /// </summary>
    public async Task GenerateFromClipsAsync(
        List<VideoSegment> segments,   // each must have ClipPath set
        string audioPath,
        string subtitleFileName,
        string? musicPath,
        string outputPath,
        double totalDuration,
        int fps = 30)
    {
        var clips = segments
            .Where(s => s.ClipPath is not null)
            .Select(s => (Path: s.ClipPath!, s.Duration))
            .ToList();

        if (clips.Count == 0)
            throw new InvalidOperationException("No clips available to render.");

        var psi = BuildPsi(outputPath);
        void Add(params string[] args) { foreach (var a in args) psi.ArgumentList.Add(a); }

        // ── Inputs: clips then audio ────────────────────────────────────────
        foreach (var (path, _) in clips) Add("-i", path);
        Add("-i", audioPath);
        if (musicPath is not null) Add("-stream_loop", "-1", "-i", musicPath);

        int audioIdx = clips.Count;       // index of TTS audio input
        int musicIdx = clips.Count + 1;   // index of music input (if present)

        // ── Filter complex ──────────────────────────────────────────────────
        var f = new List<string>();

        if (clips.Count == 1)
        {
            // Single clip — no xfade needed
            f.Add($"[0:v]setsar=1,fps={fps}[vraw]");
            f.Add($"[vraw]ass='{subtitleFileName}'[v]");
        }
        else
        {
            // Step 1: normalise each clip (already scaled/cropped during trim)
            for (int i = 0; i < clips.Count; i++)
                f.Add($"[{i}:v]setsar=1,fps={fps}[c{i}]");

            // Step 2: chain xfade transitions
            // offset[i] = sum(dur[0..i]) - xfade * (i+1)
            double cumulative = 0;
            string prev = "[c0]";
            for (int i = 0; i < clips.Count - 1; i++)
            {
                cumulative += clips[i].Duration;
                double offset = cumulative - XfadeDuration * (i + 1);
                string next   = $"[c{i + 1}]";
                string outTag = i == clips.Count - 2 ? "[vraw]" : $"[x{i}]";
                f.Add($"{prev}{next}xfade=transition=fade:duration={XfadeDuration:F2}:offset={offset:F3}{outTag}");
                prev = $"[x{i}]";
            }

            f.Add($"[vraw]ass='{subtitleFileName}'[v]");
        }

        // Audio mix
        string audioMap;
        if (musicPath is not null)
        {
            f.Add($"[{audioIdx}:a]volume=1.0[narr]");
            f.Add($"[{musicIdx}:a]volume=0.12[music]");
            f.Add("[narr][music]amix=inputs=2:duration=first:dropout_transition=2[audio]");
            audioMap = "[audio]";
        }
        else
        {
            audioMap = $"{audioIdx}:a";
        }

        Add("-filter_complex", string.Join(";", f));
        Add("-map", "[v]", "-map", audioMap);
        Add("-vcodec", "libx264", "-preset", "fast", "-crf", "22");
        Add("-acodec", "aac", "-ar", "44100", "-ac", "2");
        Add("-r", fps.ToString(), "-t", totalDuration.ToString("F3"));
        Add("-movflags", "+faststart");
        Add("-y", outputPath);

        await RunAsync(psi);
    }

    // ── Shared helpers ───────────────────────────────────────────────────────

    private ProcessStartInfo BuildPsi(string outputPath) => new()
    {
        FileName         = ffmpegPath,
        UseShellExecute  = false,
        RedirectStandardError = true,
        WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath))!
    };

    private static async Task RunAsync(ProcessStartInfo psi)
    {
        Console.WriteLine();
        Console.WriteLine($"  ffmpeg {string.Join(" ", psi.ArgumentList.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}");
        Console.WriteLine();

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start FFmpeg.");

        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg exited with code {process.ExitCode}:\n{stderr}");
    }
}

