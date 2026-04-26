using CarFacts.VideoPoC.Models;
using System.Speech.Synthesis;

namespace CarFacts.VideoPoC.Services;

/// <summary>
/// TTS using the built-in Windows Speech API — no API key required.
/// Uses SpeakProgress events for word-level timestamps.
/// </summary>
public class WindowsTtsService
{
    public Task<List<WordTiming>> SynthesizeAsync(string text, string outputWavPath)
    {
        var timings = new List<WordTiming>();
        var rawTimings = new List<(string Word, TimeSpan AudioPosition)>();

        using var synth = new SpeechSynthesizer();

        // Pick the best available English voice
        var voices = synth.GetInstalledVoices()
            .Where(v => v.Enabled && v.VoiceInfo.Culture.TwoLetterISOLanguageName == "en")
            .ToList();

        if (voices.Count > 0)
        {
            // Prefer a higher-quality voice if available
            var preferred = voices.FirstOrDefault(v =>
                v.VoiceInfo.Name.Contains("Zira") ||
                v.VoiceInfo.Name.Contains("David") ||
                v.VoiceInfo.Name.Contains("Mark"));
            synth.SelectVoice((preferred ?? voices[0]).VoiceInfo.Name);
        }

        synth.Rate  = -2;  // slightly slower for clarity (-10 to +10 scale)
        synth.Volume = 100;

        synth.SpeakProgress += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Text))
                rawTimings.Add((e.Text.Trim(), e.AudioPosition));
        };

        synth.SetOutputToWaveFile(outputWavPath);
        synth.Speak(text);
        synth.SetOutputToDefaultAudioDevice();

        // Build WordTimings — duration = gap to next word (or 0.4s for last word)
        for (int i = 0; i < rawTimings.Count; i++)
        {
            var (word, start) = rawTimings[i];
            var end = i < rawTimings.Count - 1
                ? rawTimings[i + 1].AudioPosition
                : start + TimeSpan.FromSeconds(0.5);

            timings.Add(new WordTiming(word, start.TotalSeconds, (end - start).TotalSeconds));
        }

        Console.WriteLine($"  Voice: {synth.Voice.Name}");
        return Task.FromResult(timings);
    }
}
