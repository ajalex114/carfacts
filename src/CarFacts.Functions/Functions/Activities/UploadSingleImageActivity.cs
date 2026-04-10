using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions.Activities;

public sealed class UploadSingleImageActivity
{
    private readonly IWordPressService _wordPressService;
    private readonly ILogger<UploadSingleImageActivity> _logger;

    public UploadSingleImageActivity(
        IWordPressService wordPressService,
        ILogger<UploadSingleImageActivity> logger)
    {
        _wordPressService = wordPressService;
        _logger = logger;
    }

    [Function(nameof(UploadSingleImageActivity))]
    public async Task<UploadedMedia> Run(
        [ActivityTrigger] UploadImageInput input)
    {
        _logger.LogInformation("Uploading image for {Model} to post {PostId}",
            input.Fact.CarModel, input.ParentPostId);

        var media = await _wordPressService.UploadSingleImageAsync(
            input.Image, input.Fact, input.ParentPostId);

        _logger.LogInformation("Uploaded: MediaId={MediaId}, URL={Url}",
            media.MediaId, media.SourceUrl);
        return media;
    }
}
