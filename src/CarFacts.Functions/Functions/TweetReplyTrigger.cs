using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions;

/// <summary>
/// HTTP trigger to generate a tweet reply. Starts the TweetReplyOrchestrator
/// which searches for a car tweet, generates a reply, and stores it in the queue.
/// Can be invoked manually or by a scheduled trigger.
/// </summary>
public sealed class TweetReplyTrigger
{
    private readonly ILogger<TweetReplyTrigger> _logger;

    public TweetReplyTrigger(ILogger<TweetReplyTrigger> logger)
    {
        _logger = logger;
    }

    [Function(nameof(TweetReplyTrigger))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Tweet reply trigger invoked at {Time}", DateTime.UtcNow);

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(TweetReplyOrchestrator),
            cancellationToken);

        _logger.LogInformation("Started TweetReplyOrchestrator: {InstanceId}", instanceId);

        var response = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new { instanceId, status = "started" }, cancellationToken);
        return response;
    }
}
