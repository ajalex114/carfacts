using CarFacts.Functions.Functions.Activities;
using CarFacts.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions;

/// <summary>
/// Orchestrator that posts a single pin to Pinterest.
/// Selects the fact with the lowest pinterestCount that has an image,
/// generates a unique title/description, posts the pin, and updates tracking.
/// </summary>
public static class PinterestPostingOrchestrator
{
    private static readonly RetryPolicy RetryPolicy = new(
        maxNumberOfAttempts: 2,
        firstRetryInterval: TimeSpan.FromSeconds(10));

    [Function(nameof(PinterestPostingOrchestrator))]
    public static async Task Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(PinterestPostingOrchestrator));

        // Step 1: Select the best fact to pin
        var selection = await context.CallActivityAsync<PinterestFactSelection?>(
            nameof(SelectPinterestFactActivity),
            "select",
            new TaskOptions(RetryPolicy));

        if (selection == null)
        {
            logger.LogInformation("No suitable facts for Pinterest — skipping this run");
            return;
        }

        var fact = selection.Fact;
        logger.LogInformation("Pinterest: pinning {Title} ({CarModel}, {Year}) to board '{Board}'",
            fact.Title, fact.CarModel, fact.Year, selection.BoardName);

        // Step 2: Generate pin title and description via LLM
        var pinContent = await context.CallActivityAsync<PinContent>(
            nameof(GeneratePinContentActivity),
            new GeneratePinContentInput
            {
                Title = fact.Title,
                CarModel = fact.CarModel,
                Year = fact.Year,
                Keywords = fact.Keywords
            },
            new TaskOptions(RetryPolicy));

        // Step 3: Create the pin on Pinterest
        var pinId = await context.CallActivityAsync<string>(
            nameof(CreatePinterestPinActivity),
            new CreatePinterestPinInput
            {
                BoardName = selection.BoardName,
                Title = pinContent.Title,
                Description = pinContent.Description,
                Link = fact.FactUrl,
                ImageUrl = fact.ImageUrl
            },
            new TaskOptions(RetryPolicy));

        // Step 4: Update tracking (increment counts + record board)
        await context.CallActivityAsync(
            nameof(UpdatePinterestTrackingActivity),
            new UpdatePinterestTrackingInput
            {
                RecordId = fact.Id,
                BoardName = selection.BoardName
            },
            new TaskOptions(RetryPolicy));

        logger.LogInformation("Pinterest pin {PinId} posted successfully for {FactId} on board '{Board}'",
            pinId, fact.Id, selection.BoardName);
    }
}
