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

    private static readonly RetryPolicy WordPressRetryPolicy = new(
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
                new TaskOptions(WordPressRetryPolicy));
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

        // Step 4: Upload images to Blob Storage FIRST (Blob URLs will be embedded in HTML)
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
                logger.LogWarning("Blob image upload failed (continuing without Blob URLs): {Message}", ex.Message);
            }
        }

        // Build UploadedMedia list with Blob SourceUrls (MediaId will be set after WP upload)
        // If Blob upload failed, SourceUrl will be empty — WP upload fills it in as fallback
        var mergedMedia = blobResults.Select(b => new UploadedMedia
        {
            FactIndex = b.FactIndex,
            MediaId = 0,          // Filled in after WP upload
            SourceUrl = b.BlobUrl // Blob URL → embedded in HTML
        }).ToList();

        FormatAndPublishResult publishResult;
        var wpUploadedMedia = new List<UploadedMedia>();

        if (images.Count > 0)
        {
            // Step 5: Create draft WP post (needed for WP media parent_id)
            var draft = await context.CallActivityAsync<WordPressPostResult>(
                nameof(CreateDraftPostActivity),
                seo.MainTitle,
                new TaskOptions(WordPressRetryPolicy));

            logger.LogInformation("WP draft post created: ID={PostId}", draft.PostId);

            // Step 6: Upload images to WP in parallel (for WP featured_image field)
            var wpUploadTasks = images.Select(img =>
                context.CallActivityAsync<UploadedMedia>(
                    nameof(UploadSingleImageActivity),
                    new UploadImageInput
                    {
                        Image = img,
                        Fact = content.Facts[img.FactIndex],
                        ParentPostId = draft.PostId
                    },
                    new TaskOptions(WordPressRetryPolicy))).ToList();

            wpUploadedMedia = (await Task.WhenAll(wpUploadTasks)).ToList();

            logger.LogInformation("Uploaded {Count} images to WordPress", wpUploadedMedia.Count);

            // Merge: use WP MediaId for featured_image, but keep Blob SourceUrl for HTML
            // If Blob upload succeeded, override SourceUrl; otherwise keep WP SourceUrl
            foreach (var wpMedia in wpUploadedMedia)
            {
                var blobMatch = mergedMedia.FirstOrDefault(m => m.FactIndex == wpMedia.FactIndex);
                if (blobMatch != null)
                {
                    blobMatch.MediaId = wpMedia.MediaId; // WP media ID for featured_image field
                    // SourceUrl stays as Blob URL (already set)
                }
                else
                {
                    // Blob upload failed for this image — use WP URL as fallback
                    mergedMedia.Add(new UploadedMedia
                    {
                        FactIndex = wpMedia.FactIndex,
                        MediaId = wpMedia.MediaId,
                        SourceUrl = wpMedia.SourceUrl
                    });
                }
            }

            // Step 7: Format HTML (with Blob URLs) + publish to WordPress
            publishResult = await context.CallActivityAsync<FormatAndPublishResult>(
                nameof(FormatAndPublishActivity),
                new PublishInput
                {
                    Content = content,
                    Seo = seo,
                    Media = mergedMedia,           // Blob SourceUrls in HTML, WP MediaId for featured_image
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

            publishResult = await context.CallActivityAsync<FormatAndPublishResult>(
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

        logger.LogInformation("WP post published: {PostUrl}", publishResult.WordPress.PostUrl);

        // Step 8: Save post to Cosmos DB 'posts' container (parallel track)
        var savePostTask = context.CallActivityAsync<bool>(
            nameof(SavePostToCosmosActivity),
            new SavePostInput
            {
                HtmlContent = publishResult.HtmlContent,
                PostUrl = canonicalPostUrl,
                WordPressPostUrl = publishResult.WordPress.PostUrl,
                WordPressPostId = publishResult.WordPress.PostId,
                Slug = slug,
                PublishedAt = publishDate,
                Content = content,
                Seo = seo,
                BlobResults = blobResults
            },
            new TaskOptions(WordPressRetryPolicy));

        // Step 9: Social media queue generation + keyword storage (best-effort, parallel)
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
                Media = mergedMedia,
                PostTitle = seo.MainTitle
            },
            new TaskOptions(WordPressRetryPolicy));

        // Step 10: Web Story creation (best-effort)
        Task<WordPressPostResult>? webStoryTask = null;
        var webStoriesEnabled = await context.CallActivityAsync<bool>(
            nameof(GetWebStoriesEnabledActivity),
            "check");
        if (webStoriesEnabled)
        {
            var featuredImageUrl = mergedMedia.Count > 0 ? mergedMedia[0].SourceUrl : string.Empty;
            webStoryTask = context.CallActivityAsync<WordPressPostResult>(
                nameof(CreateWebStoryActivity),
                new CreateWebStoryInput
                {
                    Facts = content.Facts,
                    MainTitle = seo.MainTitle,
                    PostUrl = canonicalPostUrl,
                    Excerpt = seo.MetaDescription,
                    FeaturedImageUrl = featuredImageUrl,
                    Media = mergedMedia
                },
                new TaskOptions(WordPressRetryPolicy));
        }

        // Wait for all best-effort tasks
        try { await savePostTask; }
        catch (Exception ex) { logger.LogWarning("Cosmos post save failed (non-blocking): {Message}", ex.Message); }

        try { await socialTask; }
        catch (Exception ex) { logger.LogWarning("Social media queue generation failed (non-blocking): {Message}", ex.Message); }

        try { await keywordTask; }
        catch (Exception ex) { logger.LogWarning("Keyword storage failed (non-blocking): {Message}", ex.Message); }

        // Step 11: Regenerate sitemap + RSS (best-effort, after post is saved)
        var sitemapTask = context.CallActivityAsync<bool>(
            nameof(GenerateSitemapActivity),
            "post-published",
            new TaskOptions(BlobRetryPolicy));

        var rssFeedTask = context.CallActivityAsync<bool>(
            nameof(GenerateRssFeedActivity),
            "post-published",
            new TaskOptions(BlobRetryPolicy));

        if (webStoryTask != null)
        {
            try
            {
                var storyResult = await webStoryTask;
                logger.LogInformation("Web Story published: {StoryUrl}", storyResult.PostUrl);
            }
            catch (Exception ex) { logger.LogWarning("Web Story creation failed (non-blocking): {Message}", ex.Message); }
        }

        try { await sitemapTask; }
        catch (Exception ex) { logger.LogWarning("Sitemap generation failed (non-blocking): {Message}", ex.Message); }

        try { await rssFeedTask; }
        catch (Exception ex) { logger.LogWarning("RSS feed generation failed (non-blocking): {Message}", ex.Message); }

        logger.LogInformation("CarFacts pipeline complete for {Date}: {PostUrl}", todayDate, canonicalPostUrl);
        return canonicalPostUrl;
    }
}
