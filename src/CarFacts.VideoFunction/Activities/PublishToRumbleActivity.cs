using CarFacts.VideoFunction.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Activities;

/// <summary>
/// Step 5 (Rumble) — Uploads the rendered video to a Rumble channel.
///
/// Credentials are read from app settings (backed by Key Vault):
///   Rumble:ApiKey    — Service API key from Rumble dashboard (Settings → API)
///   Rumble:ChannelId — Numeric channel ID from your Rumble channel URL
///
/// Rumble Upload API flow:
///   1. POST https://rumble.com/api/v0/user.channel.video-upload.aspx
///      (multipart/form-data with api_id, title, description, tags, visibility, video binary)
///   2. Rumble returns a video ID and the public video URL on success.
///
/// NOTE: If credentials are missing this activity returns a non-fatal error result
///       so the rest of the pipeline (tracking, scheduling) is unaffected.
/// </summary>
public class PublishToRumbleActivity(
    IConfiguration config,
    ILogger<PublishToRumbleActivity> logger)
{
    private const string UploadEndpoint = "https://rumble.com/api/v0/user.channel.video-upload.aspx";

    [Function(nameof(PublishToRumbleActivity))]
    public async Task<PublishToRumbleActivityResult> Run(
        [ActivityTrigger] PublishToRumbleActivityInput input,
        FunctionContext ctx)
    {
        var apiKey    = config["Rumble:ApiKey"];
        var channelId = config["Rumble:ChannelId"];

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(channelId))
        {
            logger.LogWarning("[{JobId}] Rumble credentials not configured — skipping publish", input.JobId);
            return new PublishToRumbleActivityResult(null, null, "Rumble credentials not configured");
        }

        logger.LogInformation("[{JobId}] PublishToRumble: downloading video from blob", input.JobId);

        var tempDir   = Path.Combine(Path.GetTempPath(), $"rumble-{input.JobId}");
        var videoPath = Path.Combine(tempDir, "video.mp4");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Download video from blob SAS URL
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            using (var stream = await http.GetStreamAsync(input.VideoUrl))
            using (var file   = File.Create(videoPath))
                await stream.CopyToAsync(file);

            var fileSize = new FileInfo(videoPath).Length;
            logger.LogInformation("[{JobId}] Video downloaded ({Size} bytes), uploading to Rumble", input.JobId, fileSize);

            var (videoId, videoUrl) = await UploadVideoAsync(http, input, videoPath, apiKey, channelId, ctx.CancellationToken);

            logger.LogInformation("[{JobId}] ✅ Published to Rumble: {Url}", input.JobId, videoUrl);
            return new PublishToRumbleActivityResult(videoId, videoUrl, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{JobId}] Rumble upload failed: {Message}", input.JobId, ex.Message);
            return new PublishToRumbleActivityResult(null, null, ex.Message);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
        }
    }

    private async Task<(string VideoId, string VideoUrl)> UploadVideoAsync(
        HttpClient http,
        PublishToRumbleActivityInput input,
        string videoPath,
        string apiKey,
        string channelId,
        CancellationToken ct)
    {
        var title       = BuildTitle(input.Fact);
        var description = BuildDescription(input.Fact, input.RelatedVideoUrl);

        logger.LogInformation("[{JobId}] Rumble title: \"{Title}\"", input.JobId, title);

        using var fileContent   = new StreamContent(File.OpenRead(videoPath));
        using var formData      = new MultipartFormDataContent();

        formData.Add(new StringContent(apiKey),                  "api_id");
        formData.Add(new StringContent(channelId),               "channel_id");
        formData.Add(new StringContent(title),                   "title");
        formData.Add(new StringContent(description),             "description");
        formData.Add(new StringContent(BuildTags()),             "tags");
        formData.Add(new StringContent("0"),                     "visibility");  // 0 = public
        formData.Add(fileContent,                                "video", "video.mp4");

        var response = await http.PostAsync(UploadEndpoint, formData, ct);
        var body     = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Rumble upload failed: HTTP {(int)response.StatusCode} — {body[..Math.Min(500, body.Length)]}");

        // Parse response JSON: { "video_id": "abc123", "video_url": "https://rumble.com/v..." }
        using var doc    = System.Text.Json.JsonDocument.Parse(body);
        var videoId      = doc.RootElement.TryGetProperty("video_id",  out var idEl)  ? idEl.GetString()  : null;
        var videoUrl     = doc.RootElement.TryGetProperty("video_url", out var urlEl) ? urlEl.GetString() : null;

        if (string.IsNullOrWhiteSpace(videoId) || string.IsNullOrWhiteSpace(videoUrl))
            throw new InvalidOperationException(
                $"Rumble upload succeeded but response missing video_id/video_url. Body: {body[..Math.Min(500, body.Length)]}");

        return (videoId!, videoUrl!);
    }

    private static string BuildTitle(string fact)
    {
        // Rumble title max = 200 chars; leave room for " #Shorts" suffix
        const int maxFactChars = 190;
        var trimmed = fact.Length > maxFactChars
            ? fact[..maxFactChars].TrimEnd() + "…"
            : fact;
        return $"{trimmed} #Shorts";
    }

    private static string BuildDescription(string fact, string? relatedVideoUrl)
    {
        var desc = fact + "\n\n" +
                   "Follow carfactsdaily.com for more daily car facts!\n\n" +
                   "#Shorts #CarFacts #Cars #Automotive #CarTrivia #DailyCarFacts #CarLovers #Supercar #Hypercar #LuxuryCars";

        if (!string.IsNullOrWhiteSpace(relatedVideoUrl))
            desc += $"\n\nRelated: {relatedVideoUrl}";

        return desc;
    }

    private static string BuildTags() =>
        "car facts,cars,shorts,automotive,car trivia,daily car facts,supercar,hypercar,luxury cars";
}
