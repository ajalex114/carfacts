using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Updates Pinterest tracking: increments pinterestCount and backlinkCount,
/// and records the board name in the fact's pinterestBoards list.
/// </summary>
public sealed class UpdatePinterestTrackingActivity
{
    private readonly IFactKeywordStore _factStore;
    private readonly ILogger<UpdatePinterestTrackingActivity> _logger;

    public UpdatePinterestTrackingActivity(
        IFactKeywordStore factStore,
        ILogger<UpdatePinterestTrackingActivity> logger)
    {
        _factStore = factStore;
        _logger = logger;
    }

    [Function(nameof(UpdatePinterestTrackingActivity))]
    public async Task Run(
        [ActivityTrigger] UpdatePinterestTrackingInput input)
    {
        _logger.LogInformation("Updating Pinterest tracking for {RecordId} on board '{Board}'",
            input.RecordId, input.BoardName);

        await _factStore.IncrementPinterestCountAsync(input.RecordId, input.BoardName);
    }
}
