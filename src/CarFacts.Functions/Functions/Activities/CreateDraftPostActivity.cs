using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

public sealed class CreateDraftPostActivity
{
    private readonly IWordPressService _wordPressService;
    private readonly ILogger<CreateDraftPostActivity> _logger;

    public CreateDraftPostActivity(
        IWordPressService wordPressService,
        ILogger<CreateDraftPostActivity> logger)
    {
        _wordPressService = wordPressService;
        _logger = logger;
    }

    [Function(nameof(CreateDraftPostActivity))]
    public async Task<WordPressPostResult> Run(
        [ActivityTrigger] string title)
    {
        _logger.LogInformation("Creating draft post: {Title}", title);
        var result = await _wordPressService.CreateDraftPostAsync(title);
        _logger.LogInformation("Draft created: PostId={PostId}", result.PostId);
        return result;
    }
}
