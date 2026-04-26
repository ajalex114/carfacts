using CarFacts.VideoFunction.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace CarFacts.VideoFunction.Functions;

/// <summary>
/// POST /api/start-video
/// Starts the Durable orchestration and returns 202 Accepted with a job ID
/// and status polling URL — responds in under 1 second, no timeout risk.
/// </summary>
public class HttpStartFunction(
    IConfiguration configuration,
    ILogger<HttpStartFunction> logger)
{
    private const string DefaultFact =
        "In 1908, Henry Ford introduced the Model T — " +
        "the first car built for ordinary people. " +
        "Ford painted every one black, " +
        "because black paint dried the fastest, keeping the assembly line moving. " +
        "At peak production, a new Model T rolled off the line every 24 seconds. " +
        "It didn't just change driving. It changed the world.";

    [Function("StartVideo")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", "get", Route = "start-video")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext ctx)
    {
        string fact = DefaultFact;
        try
        {
            var body = await req.ReadFromJsonAsync<StartVideoRequest>();
            if (!string.IsNullOrWhiteSpace(body?.Fact))
                fact = body.Fact;
        }
        catch { /* use default */ }

        var jobId = Guid.NewGuid().ToString("N")[..16]; // used as the OrchestratorInput jobId prefix

        var storageConn = configuration["Storage:ConnectionString"]
            ?? throw new InvalidOperationException("Storage:ConnectionString not configured");
        var pexelsKey   = configuration["Pexels:ApiKey"]
            ?? throw new InvalidOperationException("Pexels:ApiKey not configured");
        var youtubeKey  = configuration["YouTube:ApiKey"] ?? "";       // optional — falls back to Pexels if empty
        var visionEp    = configuration["Vision:Endpoint"] ?? "";      // optional — skips CV check if empty
        var visionKey   = configuration["Vision:ApiKey"] ?? "";

        // Schedule orchestration — Durable returns the instanceId which becomes our jobId
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(VideoOrchestrator),
            new OrchestratorInput(jobId, fact, storageConn, pexelsKey, youtubeKey, visionEp, visionKey));

        logger.LogInformation("StartVideo: instanceId={Id} fact='{Fact}'",
            instanceId, fact[..Math.Min(60, fact.Length)]);

        var statusUrl = $"{req.Url.GetLeftPart(UriPartial.Authority)}/api/status/{instanceId}?code={req.Query["code"]}";

        var response = req.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new
        {
            jobId      = instanceId,
            statusUrl,
            message    = "Video generation started. Poll statusUrl for completion.",
            fact       = fact[..Math.Min(80, fact.Length)] + (fact.Length > 80 ? "…" : "")
        });
        return response;
    }
}
