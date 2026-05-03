using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using System.Net;

namespace CarFacts.VideoFunction.Functions;

/// <summary>
/// GET /api/status/{jobId}
/// Returns the current orchestration state.
/// Poll this after calling /api/start-video until status = "Completed".
/// </summary>
public class StatusFunction(ILogger<StatusFunction> logger)
{
    [Function("GetVideoStatus")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "status/{jobId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string jobId,
        FunctionContext ctx)
    {
        logger.LogInformation("GetStatus: jobId={JobId}", jobId);

        var metadata = await client.GetInstanceAsync(jobId, getInputsAndOutputs: true);

        if (metadata is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = $"Job '{jobId}' not found." });
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);

        if (metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed)
        {
            var result = metadata.ReadOutputAs<CarFacts.VideoFunction.Models.RenderActivityResult>();
            await response.WriteAsJsonAsync(new
            {
                jobId,
                status          = "completed",
                videoUrl        = result?.VideoUrl,
                durationSecs    = result?.DurationSeconds,
                clipCount       = result?.ClipCount,
                youtubeVideoId  = result?.YouTubeVideoId,
                youtubeVideoUrl = result?.YouTubeVideoUrl,
                clips           = result?.ClipSources?.Select(c => new
                {
                    index  = c.Index,
                    source = c.Source,
                    query  = c.Query,
                    title  = c.Title,
                }),
            });
        }
        else if (metadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
        {
            await response.WriteAsJsonAsync(new
            {
                jobId,
                status = "failed",
                error  = metadata.FailureDetails?.ErrorMessage
            });
        }
        else
        {
            await response.WriteAsJsonAsync(new
            {
                jobId,
                status  = metadata.RuntimeStatus.ToString().ToLowerInvariant(),
                message = "Video is being generated. Poll again in 10–15 seconds."
            });
        }

        return response;
    }
}
