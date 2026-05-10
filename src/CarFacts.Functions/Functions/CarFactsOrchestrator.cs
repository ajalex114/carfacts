using CarFacts.Functions.Functions.Activities;
using CarFacts.Functions.Helpers;
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

    private static readonly RetryPolicy PublishRetryPolicy = new(
        maxNumberOfAttempts: 3,
        firstRetryInterval: TimeSpan.FromSeconds(3));

    private static readonly RetryPolicy BlobRetryPolicy = new(
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
        var publishDate = context.CurrentUtcDateTime;
        var todayDate = publishDate.ToString("MMMM d");

        logger.LogInformation("Starting CarFacts pipeline for {Date}", todayDate);

        // Step 1: Generate raw car facts content
        var content = await context.CallActivityAsync<RawCarFactsContent>(
            nameof(GenerateRawContentActivity),
            todayDate,
            new TaskOptions(LlmRetryPolicy));

        logger.LogInformation("Generated {Count} facts, shuffling order", content.Facts.Count);

        // Shuffle facts deterministically using orchestration context GUID
        var shuffleSeed = context.NewGuid().GetHashCode();
        var rng = new Random(shuffleSeed);
        var shuffled = content.Facts.OrderBy(_ => rng.Next()).ToList();
        content = new RawCarFactsContent { Facts = shuffled };

        logger.LogInformation("Fact order after shuffle: {Years}",
            string.Join(", ", content.Facts.Select(f => f.Year)));

        // Steps 2 & 3: SEO + images in parallel
        var seoTask = context.CallActivityAsync<SeoMetadata>(
            nameof(GenerateSeoActivity),
            content,
            new TaskOptions(LlmRetryPolicy));

        var imagesTask = context.CallActivityAsync<List<GeneratedImage>>(
            nameof(GenerateAllImagesActivity),
            content.Facts,
            new TaskOptions(ImageRetryPolicy));

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
                    CurrentPostUrl = "" // Not published yet
                },
                new TaskOptions(PublishRetryPolicy));
            logger.LogInformation("Found {Backlinks} backlinks + {Related} related posts",
                backlinksResult.Backlinks.Count, backlinksResult.RelatedPosts.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Backlink lookup failed (non-blocking): {Message}", ex.Message);
        }

        // Compute slug and canonical post URL from SEO title + date
        // This is done before any publishing so Blob paths can be set correctly.
        var slug = SlugHelper.GeneratePostSlug(seo.MainTitle);
        var datePath = publishDate.ToString("yyyy/MM/dd");
        var canonicalPostUrl = $"https://carfactsdaily.com/{datePath}/{slug}/";
        var blobPathPrefix = $"{datePath}/{slug}/";

        logger.LogInformation("Post slug: {Slug} | Canonical URL: {Url}", slug, canonicalPostUrl);

        // Step 4: Upload images to Blob Storage (Blob URLs will be embedded in HTML)
        var blobResults = new List<BlobUploadResult>();
        if (images.Count > 0)
        {
            try
            {
                blobResults = await context.CallActivityAsync<List<BlobUploadResult>>(
                    nameof(UploadImagesToBlobActivity),
                    new UploadImagesToBlobInput
                    {
                        Images = images,
                        Facts = content.Facts,
                        PathPrefix = blobPathPrefix
                    },
                    new TaskOptions(BlobRetryPolicy));

                logger.LogInformation("Uploaded {Count} images to Blob Storage", blobResults.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Blob image upload failed (continuing without images): {Message}", ex.Message);
            }
        }

        // Build media list from Blob results
        var media = blobResults.Select(b => new UploadedMedia
        {
            FactIndex = b.FactIndex,
            MediaId = 0,
            SourceUrl = b.BlobUrl
        }).ToList();

        // Step 5: Format HTML content
        var htmlContent = await context.CallActivityAsync<string>(
            nameof(FormatAndPublishActivity),
            new PublishInput
            {
                Content = content,
                Seo = seo,
                Media = media,
                TodayDate = todayDate,
                Backlinks = backlinksResult.Backlinks,
                RelatedPosts = backlinksResult.RelatedPosts
            },
            new TaskOptions(PublishRetryPolicy));

        logger.LogInformation("HTML content formatted for {Slug}", slug);

        // Step 6: Save post to Cosmos DB 'posts' container (primary publish step)
        await context.CallActivityAsync<bool>(
            nameof(SavePostToCosmosActivity),
            new SavePostInput
            {
                HtmlContent = htmlContent,
                PostUrl = canonicalPostUrl,
                Slug = slug,
                PublishedAt = publishDate,
                Content = content,
                Seo = seo,
                BlobResults = blobResults
            },
            new TaskOptions(PublishRetryPolicy));

        logger.LogInformation("Post saved to Cosmos DB: {PostUrl}", canonicalPostUrl);

        // Step 7: Social media queue generation + keyword storage (best-effort, parallel)
        var socialSettings = await context.CallActivityAsync<SocialMediaContentSettings>(
            nameof(GetSocialMediaSettingsActivity),
            "read");

        var socialTask = context.CallSubOrchestratorAsync(
            nameof(SocialMediaOrchestrator),
            new SocialMediaOrchestratorInput
            {
                PostUrl = canonicalPostUrl,
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
                PostUrl = canonicalPostUrl,
                PublishDate = publishDate,
                Backlinks = backlinksResult.Backlinks,
                RelatedPosts = backlinksResult.RelatedPosts,
                Media = media,
                PostTitle = seo.MainTitle
            },
            new TaskOptions(PublishRetryPolicy));

        // Step 8: Regenerate sitemap + RSS (best-effort, parallel)
        var sitemapTask = context.CallActivityAsync<bool>(
            nameof(GenerateSitemapActivity),
            "post-published",
            new TaskOptions(BlobRetryPolicy));

        var rssFeedTask = context.CallActivityAsync<bool>(
            nameof(GenerateRssFeedActivity),
            "post-published",
            new TaskOptions(BlobRetryPolicy));

        // Wait for all best-effort tasks
        try { await socialTask; }
        catch (Exception ex) { logger.LogWarning("Social media queue generation failed (non-blocking): {Message}", ex.Message); }

        try { await keywordTask; }
        catch (Exception ex) { logger.LogWarning("Keyword storage failed (non-blocking): {Message}", ex.Message); }

        try { await sitemapTask; }
        catch (Exception ex) { logger.LogWarning("Sitemap generation failed (non-blocking): {Message}", ex.Message); }

        try { await rssFeedTask; }
        catch (Exception ex) { logger.LogWarning("RSS feed generation failed (non-blocking): {Message}", ex.Message); }

        logger.LogInformation("CarFacts pipeline complete for {Date}: {PostUrl}", todayDate, canonicalPostUrl);
        return canonicalPostUrl;
    }
}
