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
    [Function("StartVideo")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", "get", Route = "start-video")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext ctx)
    {
        // fact is optional — if omitted, Step 0 in the orchestrator generates it via LLM
        string? fact = null;
        string? imageSearchQuery = null;
        try
        {
            var body = await req.ReadFromJsonAsync<StartVideoRequest>();
            if (!string.IsNullOrWhiteSpace(body?.Fact))
                fact = body.Fact;
            if (!string.IsNullOrWhiteSpace(body?.ImageSearchQuery))
                imageSearchQuery = body.ImageSearchQuery;
        }
        catch { /* all fields optional */ }

        var jobId = Guid.NewGuid().ToString("N")[..16]; // used as the OrchestratorInput jobId prefix

        var storageConn = configuration["Storage:ConnectionString"]
            ?? throw new InvalidOperationException("Storage:ConnectionString not configured");

        // Schedule orchestration — Durable returns the instanceId which becomes our jobId
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(VideoOrchestrator),
            new OrchestratorInput(jobId, fact, storageConn, imageSearchQuery));

        logger.LogInformation("StartVideo: instanceId={Id} fact={Fact}",
            instanceId, fact == null ? "(LLM will generate)" : fact[..Math.Min(60, fact.Length)]);

        var statusUrl = $"{req.Url.GetLeftPart(UriPartial.Authority)}/api/status/{instanceId}?code={req.Query["code"]}";

        var response = req.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new
        {
            jobId     = instanceId,
            statusUrl,
            message   = fact == null
                ? "Video generation started. Fact will be LLM-generated."
                : "Video generation started. Poll statusUrl for completion.",
            fact      = fact == null ? "(generating...)" : fact[..Math.Min(80, fact.Length)] + (fact.Length > 80 ? "…" : "")
        });
        return response;
    }
}
