using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CarFacts.VideoFunction.Functions;

/// <summary>
/// GET /api/logs/{jobId}
/// Queries App Insights traces for the given jobId and returns a structured summary:
/// per-clip source, queries, YouTube candidates found/rejected, timing.
/// </summary>
public class LogsFunction(IConfiguration configuration, ILogger<LogsFunction> logger)
{
    private static readonly HttpClient Http = new();

    // App Insights appId — from func-poc-vidgen App Insights component in rg-carfacts
    private const string AppInsightsAppId = "adf71ab0-69e2-4d63-837a-49c287cd6bad";

    [Function("GetJobLogs")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "logs/{jobId}")] HttpRequestData req,
        string jobId,
        FunctionContext ctx)
    {
        logger.LogInformation("GetJobLogs: jobId={JobId}", jobId);

        var apiKey = configuration["AppInsights:ApiKey"] ?? "";

        if (string.IsNullOrEmpty(apiKey))
        {
            // Fallback: return guidance if no API key configured
            var noKeyResp = req.CreateResponse(HttpStatusCode.OK);
            await noKeyResp.WriteAsJsonAsync(new
            {
                jobId,
                note = "App Insights API key not configured. Set AppInsights__ApiKey in function app settings.",
                tip  = $"Query manually: az monitor app-insights query --app {AppInsightsAppId} --analytics-query \"traces | where message has '{jobId}' | order by timestamp asc | project timestamp, message\""
            });
            return noKeyResp;
        }

        try
        {
            var kql = $@"traces
| where timestamp > ago(24h)
| where message has '{jobId}'
| order by timestamp asc
| project timestamp, severityLevel, message";

            var url = $"https://api.applicationinsights.io/v1/apps/{AppInsightsAppId}/query?query={Uri.EscapeDataString(kql)}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-api-key", apiKey);

            var resp = await Http.SendAsync(request);
            resp.EnsureSuccessStatusCode();
            var raw = await resp.Content.ReadFromJsonAsync<AppInsightsQueryResult>();

            var rows = raw?.Tables?.FirstOrDefault()?.Rows ?? [];

            // Parse into friendly log entries
            var entries = rows.Select(r => new
            {
                timestamp = r.Count > 0 ? r[0]?.ToString() : null,
                severity  = r.Count > 1 ? r[1]?.ToString() : null,
                message   = r.Count > 2 ? r[2]?.ToString() : null,
            }).ToList();

            // Summarize clip sources from log lines
            var clipLines = entries
                .Where(e => e.message != null &&
                            (e.message.Contains("used YouTube CC") ||
                             e.message.Contains("falling back to Pexels") ||
                             e.message.Contains("FetchClip[") ||
                             e.message.Contains("yt-dlp failed") ||
                             e.message.Contains("YouTube [")))
                .Select(e => e.message)
                .ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                jobId,
                totalLogLines = entries.Count,
                clipActivity  = clipLines,
                allLogs       = entries,
            });
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning("GetJobLogs failed: {Msg}", ex.Message);
            var errResp = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errResp.WriteAsJsonAsync(new { jobId, error = ex.Message });
            return errResp;
        }
    }

    private record AppInsightsQueryResult(
        [property: JsonPropertyName("tables")] List<AppInsightsTable>? Tables);

    private record AppInsightsTable(
        [property: JsonPropertyName("rows")] List<List<object?>> Rows);
}
