using CarFacts.Functions.Functions.Activities;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services.Interfaces;
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

        logger.LogInformation("Generated {Count} facts, shuffling order", content.Facts.Count);

        // Shuffle facts so the first fact (and thus the title image) isn't always the oldest
        // Use deterministic GUID from orchestration context to ensure replay-safety
        var shuffleSeed = context.NewGuid().GetHashCode();
        var rng = new Random(shuffleSeed);
        var shuffled = content.Facts.OrderBy(_ => rng.Next()).ToList();
        content = new RawCarFactsContent { Facts = shuffled };

        logger.LogInformation("Fact order after shuffle: {Years}",
            string.Join(", ", content.Facts.Select(f => f.Year)));

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

        // Step 3.5: Find related backlinks + related posts from Cosmos DB
        var backlinksResult = new BacklinksResult();
        try
        {
            backlinksResult = await context.CallActivityAsync<BacklinksResult>(
                nameof(FindBacklinksActivity),
                new FindBacklinksInput
                {
                    FactKeywords = seo.FactKeywords,
                    CurrentPostUrl = "" // Not published yet — no URL to exclude
                },
                new TaskOptions(WordPressRetryPolicy));
            logger.LogInformation("Found {Backlinks} backlinks + {Related} related posts",
                backlinksResult.Backlinks.Count, backlinksResult.RelatedPosts.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Backlink lookup failed (non-blocking): {Message}", ex.Message);
        }

        WordPressPostResult publishResult;
        var uploadedMedia = new List<UploadedMedia>();

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

            uploadedMedia = (await Task.WhenAll(uploadTasks)).ToList();

            logger.LogInformation("Uploaded {Count} images to WordPress", uploadedMedia.Count);

            // Step 6: Format HTML, update draft, and publish
            publishResult = await context.CallActivityAsync<WordPressPostResult>(
                nameof(FormatAndPublishActivity),
                new PublishInput
                {
                    Content = content,
                    Seo = seo,
                    Media = uploadedMedia,
                    TodayDate = todayDate,
                    DraftPostId = draft.PostId,
                    Backlinks = backlinksResult.Backlinks,
                    RelatedPosts = backlinksResult.RelatedPosts
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
                    DraftPostId = 0,
                    Backlinks = backlinksResult.Backlinks,
                    RelatedPosts = backlinksResult.RelatedPosts
                },
                new TaskOptions(WordPressRetryPolicy));
        }

        logger.LogInformation("Post published: {PostUrl}", publishResult.PostUrl);

        // Step 7: Social media queue generation + keyword storage (best-effort, parallel)
        var socialSettings = await context.CallActivityAsync<SocialMediaContentSettings>(
            nameof(GetSocialMediaSettingsActivity),
            "read");

        var socialTask = context.CallSubOrchestratorAsync(
            nameof(SocialMediaOrchestrator),
            new SocialMediaOrchestratorInput
            {
                PostUrl = publishResult.PostUrl,
                PostTitle = seo.MainTitle,
                FactsPerDay = socialSettings.FactsPerDay,
                LinkPostsPerDay = socialSettings.LinkPostsPerDay,
                LikesEnabled = socialSettings.LikesEnabled,
                RepliesEnabled = socialSettings.RepliesEnabled,
                LikesPerDayMin = socialSettings.LikesPerDayMin,
                LikesPerDayMax = socialSettings.LikesPerDayMax,
                RepliesPerDayMin = socialSettings.RepliesPerDayMin,
                RepliesPerDayMax = socialSettings.RepliesPerDayMax
            });

        var keywordTask = context.CallActivityAsync<bool>(
            nameof(StoreFactKeywordsActivity),
            new StoreFactKeywordsInput
            {
                Content = content,
                Seo = seo,
                PostUrl = publishResult.PostUrl,
                PublishDate = context.CurrentUtcDateTime,
                Backlinks = backlinksResult.Backlinks,
                RelatedPosts = backlinksResult.RelatedPosts,
                Media = uploadedMedia,
                PostTitle = seo.MainTitle
            },
            new TaskOptions(WordPressRetryPolicy));

        try { await socialTask; }
        catch (Exception ex) { logger.LogWarning("Social media queue generation failed (non-blocking): {Message}", ex.Message); }

        try { await keywordTask; }
        catch (Exception ex) { logger.LogWarning("Keyword storage failed (non-blocking): {Message}", ex.Message); }

        logger.LogInformation("CarFacts pipeline complete for {Date}: {PostUrl}", todayDate, publishResult.PostUrl);
        return publishResult.PostUrl;
    }
}
