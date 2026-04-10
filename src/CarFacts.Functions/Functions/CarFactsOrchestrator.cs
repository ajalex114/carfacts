using CarFacts.Functions.Functions.Activities;
using CarFacts.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CarFacts.Functions.Functions;

public static class CarFactsOrchestrator
{
    private static readonly RetryPolicy LlmRetryPolicy = new(
        maxNumberOfAttempts: 3,
        firstRetryInterval: TimeSpan.FromSeconds(5));

    private static readonly RetryPolicy ImageRetryPolicy = new(
        maxNumberOfAttempts: 3,
        firstRetryInterval: TimeSpan.FromSeconds(10));

    private static readonly RetryPolicy WordPressRetryPolicy = new(
        maxNumberOfAttempts: 3,
        firstRetryInterval: TimeSpan.FromSeconds(3));

    private static readonly RetryPolicy SocialRetryPolicy = new(
        maxNumberOfAttempts: 2,
        firstRetryInterval: TimeSpan.FromSeconds(5));

    [Function(nameof(CarFactsOrchestrator))]
    public static async Task<string> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(CarFactsOrchestrator));
        var todayDate = context.CurrentUtcDateTime.ToString("MMMM d");

        logger.LogInformation("Starting CarFacts pipeline for {Date}", todayDate);

        // Step 1: Generate raw car facts content
        var content = await context.CallActivityAsync<RawCarFactsContent>(
            nameof(GenerateRawContentActivity),
            todayDate,
            new TaskOptions(LlmRetryPolicy));

        logger.LogInformation("Generated {Count} facts, starting SEO + image generation", content.Facts.Count);

        // Steps 2 & 3: SEO + images in parallel
        // SEO runs as one LLM call; images run sequentially inside one activity
        // (image APIs rate-limit, so per-image fan-out causes 429 failures)
        var seoTask = context.CallActivityAsync<SeoMetadata>(
            nameof(GenerateSeoActivity),
            content,
            new TaskOptions(LlmRetryPolicy));

        var imagesTask = context.CallActivityAsync<List<GeneratedImage>>(
            nameof(GenerateAllImagesActivity),
            content.Facts,
            new TaskOptions(ImageRetryPolicy));

        // Wait for both to complete
        await Task.WhenAll(seoTask, imagesTask);

        var seo = seoTask.Result;
        var images = imagesTask.Result;

        logger.LogInformation("SEO: {Title} | {ImageCount} images generated", seo.MainTitle, images.Count);

        WordPressPostResult publishResult;

        if (images.Count > 0)
        {
            // Step 4: Create draft post (so we have a parent_id for image uploads)
            var draft = await context.CallActivityAsync<WordPressPostResult>(
                nameof(CreateDraftPostActivity),
                seo.MainTitle,
                new TaskOptions(WordPressRetryPolicy));

            logger.LogInformation("Draft post created: ID={PostId}", draft.PostId);

            // Step 5: Upload images in parallel with parent_id (fan-out)
            var uploadTasks = images.Select(img =>
                context.CallActivityAsync<UploadedMedia>(
                    nameof(UploadSingleImageActivity),
                    new UploadImageInput
                    {
                        Image = img,
                        Fact = content.Facts[img.FactIndex],
                        ParentPostId = draft.PostId
                    },
                    new TaskOptions(WordPressRetryPolicy))).ToList();

            var media = (await Task.WhenAll(uploadTasks)).ToList();

            logger.LogInformation("Uploaded {Count} images to WordPress", media.Count);

            // Step 6: Format HTML, update draft, and publish
            publishResult = await context.CallActivityAsync<WordPressPostResult>(
                nameof(FormatAndPublishActivity),
                new PublishInput
                {
                    Content = content,
                    Seo = seo,
                    Media = media,
                    TodayDate = todayDate,
                    DraftPostId = draft.PostId
                },
                new TaskOptions(WordPressRetryPolicy));
        }
        else
        {
            // No images — publish text-only directly
            logger.LogWarning("No images generated — publishing text-only post");

            publishResult = await context.CallActivityAsync<WordPressPostResult>(
                nameof(FormatAndPublishActivity),
                new PublishInput
                {
                    Content = content,
                    Seo = seo,
                    Media = [],
                    TodayDate = todayDate,
                    DraftPostId = 0
                },
                new TaskOptions(WordPressRetryPolicy));
        }

        logger.LogInformation("Post published: {PostUrl}", publishResult.PostUrl);

        // Step 7: Social media (best-effort, fire-and-forget style)
        try
        {
            await context.CallActivityAsync<bool>(
                nameof(PublishSocialMediaActivity),
                new SocialPublishInput
                {
                    Teaser = seo.SocialMediaTeaser,
                    PostUrl = publishResult.PostUrl,
                    Title = seo.MainTitle,
                    Hashtags = seo.SocialMediaHashtags
                },
                new TaskOptions(SocialRetryPolicy));
        }
        catch (Exception ex)
        {
            logger.LogWarning("Social media publishing failed (non-blocking): {Message}", ex.Message);
        }

        logger.LogInformation("CarFacts pipeline complete for {Date}: {PostUrl}", todayDate, publishResult.PostUrl);
        return publishResult.PostUrl;
    }
}
