using CarFacts.VideoFunction.Models;
using CarFacts.VideoFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CarFacts.VideoFunction.Activities;

/// <summary>
/// Activity 0 — Generates a single ~50-word car fact for video narration using LLM.
/// Runs as the first step in the orchestrator when no fact is provided by the caller.
/// Prompt is tuned independently for short-form video in Prompts/CarFactVideoPrompt.txt.
/// </summary>
public class GenerateCarFactActivity(
    CarFactGenerationService factService,
    ILogger<GenerateCarFactActivity> logger)
{
    [Function(nameof(GenerateCarFactActivity))]
    public async Task<GenerateCarFactActivityResult> Run(
        [ActivityTrigger] GenerateCarFactActivityInput input,
        FunctionContext ctx)
    {
        logger.LogInformation("[{JobId}] GenerateCarFact: generating via LLM", input.JobId);
        var fact = await factService.GenerateFactAsync();
        logger.LogInformation("[{JobId}] GenerateCarFact: → \"{Preview}\"",
            input.JobId, fact[..Math.Min(80, fact.Length)]);
        return new GenerateCarFactActivityResult(fact);
    }
}
