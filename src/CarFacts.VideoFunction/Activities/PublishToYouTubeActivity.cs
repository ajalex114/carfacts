using CarFacts.VideoFunction.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Activities;

/// <summary>
/// Step 5 — Uploads the rendered video to the YouTube channel.
/// Reads OAuth credentials from app settings (YouTube:ClientId / ClientSecret / RefreshToken).
/// If credentials are missing, logs a warning and returns null (non-fatal).
/// </summary>
public class PublishToYouTubeActivity(
    IConfiguration config,
    ILogger<PublishToYouTubeActivity> logger)
{
    [Function(nameof(PublishToYouTubeActivity))]
    public async Task<PublishToYouTubeActivityResult> Run(
        [ActivityTrigger] PublishToYouTubeActivityInput input,
        FunctionContext ctx)
    {
        var clientId     = config["YouTube:ClientId"];
        var clientSecret = config["YouTube:ClientSecret"];
        var refreshToken = config["YouTube:RefreshToken"];

        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            logger.LogWarning("[{JobId}] YouTube credentials not configured — skipping publish", input.JobId);
            return new PublishToYouTubeActivityResult(null, null, "YouTube credentials not configured");
        }

        logger.LogInformation("[{JobId}] PublishToYouTube: downloading video from blob", input.JobId);

        var tempDir   = Path.Combine(Path.GetTempPath(), $"yt-{input.JobId}");
        var videoPath = Path.Combine(tempDir, "video.mp4");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Download video from blob SAS URL
            using (var http = new HttpClient())
            using (var stream = await http.GetStreamAsync(input.VideoUrl))
            using (var file   = File.Create(videoPath))
                await stream.CopyToAsync(file);

            logger.LogInformation("[{JobId}] Video downloaded ({Size} bytes), uploading to YouTube",
                input.JobId, new FileInfo(videoPath).Length);

            var youtubeService = CreateYouTubeService(clientId, clientSecret, refreshToken);
            var videoId        = await UploadVideoAsync(youtubeService, input, videoPath, ctx.CancellationToken);

            var youtubeUrl = $"https://youtu.be/{videoId}";
            logger.LogInformation("[{JobId}] ✅ Published to YouTube: {Url}", input.JobId, youtubeUrl);
            return new PublishToYouTubeActivityResult(videoId, youtubeUrl, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{JobId}] YouTube upload failed: {Message}", input.JobId, ex.Message);
            return new PublishToYouTubeActivityResult(null, null, ex.Message);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
        }
    }

    private static YouTubeService CreateYouTubeService(string clientId, string clientSecret, string refreshToken)
    {
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
            Scopes        = [YouTubeService.Scope.YoutubeUpload]
        });

        var credential = new UserCredential(flow, "user", new TokenResponse
        {
            RefreshToken = refreshToken
        });

        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName       = "CarFactsDaily"
        });
    }

    private async Task<string> UploadVideoAsync(
        YouTubeService yt,
        PublishToYouTubeActivityInput input,
        string videoPath,
        CancellationToken ct)
    {
        var title       = BuildTitle(input.Fact);
        var description = BuildDescription(input.Fact);

        var video = new Video
        {
            Snippet = new VideoSnippet
            {
                Title       = title,
                Description = description,
                Tags        = ["car facts", "cars", "shorts", "automotive", "car trivia", "daily car facts"],
                CategoryId  = "2"   // Autos & Vehicles
            },
            Status = new VideoStatus
            {
                PrivacyStatus        = "public",
                SelfDeclaredMadeForKids = false
            }
        };

        logger.LogInformation("[{JobId}] YouTube title: \"{Title}\"", input.JobId, title);

        using var fileStream = File.OpenRead(videoPath);
        var insertRequest    = yt.Videos.Insert(video, "snippet,status", fileStream, "video/mp4");

        insertRequest.ProgressChanged  += p => logger.LogDebug("[{JobId}] Upload: {Status} {Bytes}b",
            input.JobId, p.Status, p.BytesSent);
        insertRequest.ResponseReceived += v => logger.LogInformation("[{JobId}] Upload complete, videoId={Id}",
            input.JobId, v.Id);

        var result = await insertRequest.UploadAsync(ct);
        if (result.Status != UploadStatus.Completed)
            throw new InvalidOperationException($"Upload did not complete: {result.Status} — {result.Exception?.Message}");

        return insertRequest.ResponseBody.Id;
    }

    private static string BuildTitle(string fact)
    {
        // YouTube title max = 100 chars; leave room for " #Shorts" (8 chars)
        const int maxFactChars = 90;
        var trimmed = fact.Length > maxFactChars
            ? fact[..maxFactChars].TrimEnd() + "…"
            : fact;
        return $"{trimmed} #Shorts";
    }

    private static string BuildDescription(string fact) =>
        fact + "\n\n" +
        "Follow carfactsdaily.com for more daily car facts!\n\n" +
        "#Shorts #CarFacts #Cars #Automotive #CarTrivia #DailyCarFacts #CarLovers #Supercar #Hypercar #LuxuryCars";
}
