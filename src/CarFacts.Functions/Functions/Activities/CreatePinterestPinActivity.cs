using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

/// <summary>
/// Creates a pin on Pinterest using the Pinterest API v5.
/// Resolves the board by name (creating it if necessary).
/// </summary>
public sealed class CreatePinterestPinActivity
{
    private readonly IPinterestService _pinterestService;
    private readonly ILogger<CreatePinterestPinActivity> _logger;

    public CreatePinterestPinActivity(
        IPinterestService pinterestService,
        ILogger<CreatePinterestPinActivity> logger)
    {
        _pinterestService = pinterestService;
        _logger = logger;
    }

    [Function(nameof(CreatePinterestPinActivity))]
    public async Task<string> Run(
        [ActivityTrigger] CreatePinterestPinInput input)
    {
        _logger.LogInformation("Creating Pinterest pin on board '{Board}' for '{Title}'",
            input.BoardName, input.Title);

        var boardId = await _pinterestService.GetOrCreateBoardAsync(input.BoardName);

        var pinId = await _pinterestService.CreatePinAsync(
            boardId,
            input.Title,
            input.Description,
            input.Link,
            input.ImageUrl);

        _logger.LogInformation("Pinterest pin created: {PinId}", pinId);
        return pinId;
    }
}
