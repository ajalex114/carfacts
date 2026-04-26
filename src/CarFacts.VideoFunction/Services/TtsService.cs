using CarFacts.VideoFunction.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace CarFacts.VideoFunction.Services;

public class TtsService(string subscriptionKey, string region, string voiceName = "en-US-AndrewNeural")
{
    public async Task<List<WordTiming>> SynthesizeAsync(string text, string outputWavPath)
    {
        var config = SpeechConfig.FromSubscription(subscriptionKey, region);
        config.SpeechSynthesisVoiceName = voiceName;

        using var audioConfig = AudioConfig.FromWavFileOutput(outputWavPath);
        using var synthesizer = new SpeechSynthesizer(config, audioConfig);

        var timings = new List<WordTiming>();

        synthesizer.WordBoundary += (_, e) =>
        {
            if (e.BoundaryType == SpeechSynthesisBoundaryType.Word)
                timings.Add(new WordTiming(e.Text, e.AudioOffset / 10_000_000.0, e.Duration.TotalSeconds));
        };

        var escapedText = text
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\"", "&quot;").Replace("'", "&apos;");

        var ssml = $"""
            <speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>
                <voice name='{voiceName}'>
                    <prosody rate='0.88'>{escapedText}</prosody>
                </voice>
            </speak>
            """;

        var result = await synthesizer.SpeakSsmlAsync(ssml);

        if (result.Reason != ResultReason.SynthesizingAudioCompleted)
        {
            var detail = SpeechSynthesisCancellationDetails.FromResult(result);
            throw new InvalidOperationException($"TTS failed: {detail.Reason} — {detail.ErrorDetails}");
        }

        return timings;
    }
}

