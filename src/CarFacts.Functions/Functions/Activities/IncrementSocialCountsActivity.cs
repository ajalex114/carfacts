using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

public sealed class IncrementSocialCountsActivity
{
    private readonly IFactKeywordStore _factKeywordStore;
    private readonly ILogger<IncrementSocialCountsActivity> _logger;

    public IncrementSocialCountsActivity(
        IFactKeywordStore factKeywordStore,
        ILogger<IncrementSocialCountsActivity> logger)
    {
        _factKeywordStore = factKeywordStore;
        _logger = logger;
    }

    [Function(nameof(IncrementSocialCountsActivity))]
    public async Task<bool> RunAsync(
        [ActivityTrigger] IncrementSocialCountsInput input)
    {
        _logger.LogInformation("Incrementing {Platform} counts for {PostUrl}", input.Platform, input.PostUrl);

        await _factKeywordStore.IncrementSocialCountsAsync(input.PostUrl, input.Platform);

        return true;
    }
}
