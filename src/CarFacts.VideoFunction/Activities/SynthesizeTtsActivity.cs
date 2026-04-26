using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CarFacts.VideoFunction.Models;
using CarFacts.VideoFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Activities;

/// <summary>
/// Activity 1 — Synthesizes TTS narration and generates ASS subtitles.
/// Uploads the WAV to blob storage and returns the audio URL + word timings.
/// Each activity has its own temp dir, isolated from all other activities.
/// </summary>
public class SynthesizeTtsActivity(
    TtsService ttsService,
    SubtitleGenerator subtitleGenerator,
    ILogger<SynthesizeTtsActivity> logger)
{
    [Function(nameof(SynthesizeTtsActivity))]
    public async Task<TtsActivityResult> Run(
        [ActivityTrigger] TtsActivityInput input,
        FunctionContext ctx)
    {
        logger.LogInformation("[{JobId}] SynthesizeTts: synthesizing {Len} chars", input.JobId, input.Fact.Length);

        var tempDir = Path.Combine(Path.GetTempPath(), $"tts-{input.JobId}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var wavPath = Path.Combine(tempDir, "narration.wav");
            var words   = await ttsService.SynthesizeAsync(input.Fact, wavPath);
            logger.LogInformation("[{JobId}] TTS done: {Count} words", input.JobId, words.Count);

            var narrationEnd  = words[^1].EndSeconds;
            var totalDuration = narrationEnd + 2.3;

            var assText = subtitleGenerator.GenerateAss(words, totalDuration, "carfactsdaily.com");

            // Upload WAV to blob: poc-jobs/{jobId}/narration.wav
            var audioUrl = await UploadToBlobAsync(
                input.StorageConnectionString, wavPath,
                $"poc-jobs/{input.JobId}/narration.wav");

            logger.LogInformation("[{JobId}] Audio uploaded: {Url}", input.JobId, audioUrl[..60]);
            return new TtsActivityResult(audioUrl, assText, words, totalDuration);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static async Task<string> UploadToBlobAsync(
        string connectionString, string filePath, string blobPath)
    {
        var parts     = blobPath.Split('/', 2);
        var container = new BlobContainerClient(connectionString, parts[0]);
        await container.CreateIfNotExistsAsync(PublicAccessType.None);

        var blob = container.GetBlobClient(parts[1]);
        await using var stream = File.OpenRead(filePath);
        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "audio/wav" }
        });

        var sasUri = blob.GenerateSasUri(
            Azure.Storage.Sas.BlobSasPermissions.Read,
            DateTimeOffset.UtcNow.AddHours(4));
        return sasUri.ToString();
    }
}
