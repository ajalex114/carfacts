using CarFacts.VideoPoC.Models;
using System.Diagnostics;
using System.Text.Json;

namespace CarFacts.VideoPoC.Services;

/// <summary>
/// TTS using edge-tts (Microsoft Edge neural voices, free) for audio quality,
/// combined with Whisper "tiny" for precise word-level timestamps.
///
/// Requires: pip install edge-tts openai-whisper
/// </summary>
public class EdgeTtsService(
    string pythonPath,
    string scriptPath,
    string ffmpegDir,
    string voice = "en-US-AndrewNeural")
{
    public async Task<List<WordTiming>> SynthesizeAsync(string text, string outputMp3Path)
    {
        var jsonPath = Path.ChangeExtension(outputMp3Path, ".words.json");

        var psi = new ProcessStartInfo
        {
            FileName         = pythonPath,
            UseShellExecute  = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
        };

        foreach (var arg in new[] { scriptPath, text, voice, outputMp3Path, jsonPath, ffmpegDir })
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start Python. Is it installed?");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"tts_timing.py failed (exit {process.ExitCode}):\n{stderr}\n{stdout}");

        // stdout format: "ok:<wordCount>:<totalSeconds>"
        var summary = stdout.Trim();
        if (!summary.StartsWith("ok:"))
            throw new InvalidOperationException($"Unexpected tts_timing.py output: {summary}\n{stderr}");

        // Parse word timings from JSON
        var jsonText = await File.ReadAllTextAsync(jsonPath);
        var options  = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var raw = JsonSerializer.Deserialize<List<WordTimingJson>>(jsonText, options)
            ?? throw new InvalidOperationException("Empty word timing JSON");

        return raw.Select(w => new WordTiming(w.Word, w.Start, w.End - w.Start)).ToList();
    }

    private record WordTimingJson(string Word, double Start, double End);
}
