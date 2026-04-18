using CarFacts.Functions.Functions.Activities;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services;
using CarFacts.Functions.Services.Interfaces;
using CarFacts.Functions.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CarFacts.Functions.Tests.Functions;

public class ActivityTests
{
    #region GenerateRawContentActivity

    [Fact]
    public async Task GenerateRawContent_ReturnsContentFromService()
    {
        var expected = TestDataBuilder.CreateValidRawContent();
        var contentService = new Mock<IContentGenerationService>();
        contentService
            .Setup(s => s.GenerateFactsAsync("March 21", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var activity = new GenerateRawContentActivity(
            contentService.Object,
            Mock.Of<ILogger<GenerateRawContentActivity>>());

        var result = await activity.Run("March 21");

        result.Facts.Should().HaveCount(5);
        result.Facts[0].CarModel.Should().Be(expected.Facts[0].CarModel);
    }

    #endregion

    #region GenerateSeoActivity

    [Fact]
    public async Task GenerateSeo_ReturnsSeoFromService()
    {
        var content = TestDataBuilder.CreateValidRawContent();
        var expectedSeo = TestDataBuilder.CreateValidSeoMetadata();
        var seoService = new Mock<ISeoGenerationService>();
        seoService
            .Setup(s => s.GenerateSeoAsync(content, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSeo);

        var activity = new GenerateSeoActivity(
            seoService.Object,
            Mock.Of<ILogger<GenerateSeoActivity>>());

        var result = await activity.Run(content);

        result.MainTitle.Should().Be(expectedSeo.MainTitle);
        result.MetaDescription.Should().Be(expectedSeo.MetaDescription);
        result.Keywords.Should().HaveCountGreaterThan(0);
    }

    #endregion

    #region GenerateAllImagesActivity

    [Fact]
    public async Task GenerateAllImages_WhenSuccessful_ReturnsImages()
    {
        var facts = TestDataBuilder.CreateValidRawContent().Facts;
        var expectedImages = TestDataBuilder.CreateGeneratedImages();

        var imageService = new Mock<IImageGenerationService>();
        imageService
            .Setup(s => s.GenerateImagesAsync(facts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedImages);

        var activity = new GenerateAllImagesActivity(
            imageService.Object,
            Mock.Of<ILogger<GenerateAllImagesActivity>>());

        var result = await activity.Run(facts);

        result.Should().HaveCount(5);
        result[0].FactIndex.Should().Be(0);
        result[4].FactIndex.Should().Be(4);
    }

    [Fact]
    public async Task GenerateAllImages_WhenFails_ReturnsEmptyList()
    {
        var facts = TestDataBuilder.CreateValidRawContent().Facts;
        var imageService = new Mock<IImageGenerationService>();
        imageService
            .Setup(s => s.GenerateImagesAsync(It.IsAny<List<CarFact>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Rate limited"));

        var activity = new GenerateAllImagesActivity(
            imageService.Object,
            Mock.Of<ILogger<GenerateAllImagesActivity>>());

        var result = await activity.Run(facts);

        result.Should().BeEmpty();
    }

    #endregion

    #region CreateDraftPostActivity

    [Fact]
    public async Task CreateDraftPost_ReturnsDraftPostResult()
    {
        var expected = TestDataBuilder.CreatePostResult();
        var wpService = new Mock<IWordPressService>();
        wpService
            .Setup(s => s.CreateDraftPostAsync("Test Title", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var activity = new CreateDraftPostActivity(
            wpService.Object,
            Mock.Of<ILogger<CreateDraftPostActivity>>());

        var result = await activity.Run("Test Title");

        result.PostId.Should().Be(42);
    }

    #endregion

    #region UploadSingleImageActivity

    [Fact]
    public async Task UploadSingleImage_ReturnsUploadedMedia()
    {
        var fact = TestDataBuilder.CreateFact(0);
        var image = new GeneratedImage { FactIndex = 0, ImageData = [0x89], FileName = "test.png" };
        var expectedMedia = new UploadedMedia { FactIndex = 0, MediaId = 100, SourceUrl = "https://example.com/test.png" };
        var input = new UploadImageInput { Image = image, Fact = fact, ParentPostId = 42 };

        var wpService = new Mock<IWordPressService>();
        wpService
            .Setup(s => s.UploadSingleImageAsync(image, fact, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMedia);

        var activity = new UploadSingleImageActivity(
            wpService.Object,
            Mock.Of<ILogger<UploadSingleImageActivity>>());

        var result = await activity.Run(input);

        result.MediaId.Should().Be(100);
        result.SourceUrl.Should().Be("https://example.com/test.png");
    }

    #endregion

    #region FormatAndPublishActivity

    [Fact]
    public async Task FormatAndPublish_WithDraftPost_UpdatesAndPublishes()
    {
        var content = TestDataBuilder.CreateValidRawContent();
        var seo = TestDataBuilder.CreateValidSeoMetadata();
        var media = TestDataBuilder.CreateUploadedMedia();
        var expected = TestDataBuilder.CreatePostResult();

        var formatter = new Mock<IContentFormatterService>();
        formatter
            .Setup(s => s.FormatPostHtml(It.IsAny<RawCarFactsContent>(), It.IsAny<SeoMetadata>(), It.IsAny<List<UploadedMedia>>(), It.IsAny<string>(), It.IsAny<List<BacklinkSuggestion>>(), It.IsAny<List<RelatedPostSuggestion>>()))
            .Returns("<p>HTML</p>");

        var wpService = new Mock<IWordPressService>();
        wpService
            .Setup(s => s.UpdateAndPublishPostAsync(
                42, It.IsAny<string>(), "<p>HTML</p>", It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var activity = new FormatAndPublishActivity(
            formatter.Object,
            wpService.Object,
            Mock.Of<ILogger<FormatAndPublishActivity>>());

        var input = new PublishInput
        {
            Content = content,
            Seo = seo,
            Media = media,
            TodayDate = "March 21",
            DraftPostId = 42
        };

        var result = await activity.Run(input);

        result.PostId.Should().Be(42);
        wpService.Verify(s => s.UpdateAndPublishPostAsync(
            42, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FormatAndPublish_WithNoDraft_CreatesNewPost()
    {
        var content = TestDataBuilder.CreateValidRawContent();
        var seo = TestDataBuilder.CreateValidSeoMetadata();
        var expected = TestDataBuilder.CreatePostResult();

        var formatter = new Mock<IContentFormatterService>();
        formatter
            .Setup(s => s.FormatPostHtml(It.IsAny<RawCarFactsContent>(), It.IsAny<SeoMetadata>(), It.IsAny<List<UploadedMedia>>(), It.IsAny<string>(), It.IsAny<List<BacklinkSuggestion>>(), It.IsAny<List<RelatedPostSuggestion>>()))
            .Returns("<p>HTML</p>");

        var wpService = new Mock<IWordPressService>();
        wpService
            .Setup(s => s.CreatePostAsync(
                It.IsAny<string>(), "<p>HTML</p>", It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var activity = new FormatAndPublishActivity(
            formatter.Object,
            wpService.Object,
            Mock.Of<ILogger<FormatAndPublishActivity>>());

        var input = new PublishInput
        {
            Content = content,
            Seo = seo,
            Media = [],
            TodayDate = "March 21",
            DraftPostId = 0
        };

        var result = await activity.Run(input);

        result.PostId.Should().Be(42);
        wpService.Verify(s => s.CreatePostAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region PublishSocialMediaActivity

    [Fact]
    public async Task PublishSocialMedia_WhenSuccessful_ReturnsTrue()
    {
        var publisher = new CarFacts.Functions.Services.SocialMediaPublisher(
            Enumerable.Empty<ISocialMediaService>(),
            Mock.Of<ILogger<CarFacts.Functions.Services.SocialMediaPublisher>>());

        var activity = new PublishSocialMediaActivity(
            publisher,
            Mock.Of<ILogger<PublishSocialMediaActivity>>());

        var input = new SocialPublishInput
        {
            Teaser = "Test teaser",
            PostUrl = "https://example.com",
            Title = "Test",
            Hashtags = ["#test"]
        };

        var result = await activity.Run(input);

        result.Should().BeTrue();
    }

    #endregion

    #region StoreFactKeywordsActivity

    [Fact]
    public async Task StoreFactKeywords_CallsStoreWithCorrectRecords()
    {
        var content = TestDataBuilder.CreateValidRawContent();
        var seo = TestDataBuilder.CreateValidSeoMetadata();
        var store = new Mock<IFactKeywordStore>();

        var activity = new StoreFactKeywordsActivity(
            store.Object,
            Mock.Of<ILogger<StoreFactKeywordsActivity>>());

        var input = new StoreFactKeywordsInput
        {
            Content = content,
            Seo = seo,
            PostUrl = "https://example.com/test-post/",
            PublishDate = new DateTime(2026, 3, 21, 6, 0, 0, DateTimeKind.Utc)
        };

        var result = await activity.Run(input);

        result.Should().BeTrue();
        store.Verify(s => s.UpsertFactsAsync(
            It.Is<IEnumerable<FactKeywordRecord>>(r => r.Count() == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StoreFactKeywords_GeneratesCorrectAnchorIds()
    {
        var content = TestDataBuilder.CreateValidRawContent();
        var seo = TestDataBuilder.CreateValidSeoMetadata();
        IEnumerable<FactKeywordRecord>? storedRecords = null;

        var store = new Mock<IFactKeywordStore>();
        store.Setup(s => s.UpsertFactsAsync(It.IsAny<IEnumerable<FactKeywordRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<FactKeywordRecord>, CancellationToken>((r, _) => storedRecords = r.ToList())
            .Returns(Task.CompletedTask);

        var activity = new StoreFactKeywordsActivity(
            store.Object,
            Mock.Of<ILogger<StoreFactKeywordsActivity>>());

        var input = new StoreFactKeywordsInput
        {
            Content = content,
            Seo = seo,
            PostUrl = "https://example.com/test-post/",
            PublishDate = new DateTime(2026, 3, 21, 6, 0, 0, DateTimeKind.Utc)
        };

        await activity.Run(input);

        storedRecords.Should().NotBeNull();
        var records = storedRecords!.ToList();

        // Ford Model 48, 1935 → "ford-model-48-1935"
        records[0].AnchorId.Should().Be("ford-model-48-1935");
        records[0].Id.Should().Be("2026-03-21_ford-model-48-1935");
        records[0].FactUrl.Should().Be("https://example.com/test-post/#ford-model-48-1935");
        records[0].Keywords.Should().Contain("ford");

        // BMW 3.0 CSL, 1972 → "bmw-3-0-csl-1972"
        records[2].AnchorId.Should().Be("bmw-3-0-csl-1972");
    }

    #endregion

    #region StoreSocialMediaQueueActivity

    [Fact]
    public async Task StoreSocialQueue_StoresCorrectItemCount()
    {
        var queueStore = new Mock<ISocialMediaQueueStore>();
        var capturedItems = new List<SocialMediaQueueItem>();
        queueStore.Setup(s => s.AddItemsAsync(It.IsAny<IEnumerable<SocialMediaQueueItem>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<SocialMediaQueueItem>, CancellationToken>((items, _) => capturedItems.AddRange(items))
            .Returns(Task.CompletedTask);

        var activity = new StoreSocialMediaQueueActivity(
            queueStore.Object,
            Mock.Of<ILogger<StoreSocialMediaQueueActivity>>());

        var input = new StoreSocialQueueInput
        {
            Facts = [
                new TweetFactResult { Text = "Fact 1", Hashtags = ["#test1"] },
                new TweetFactResult { Text = "Fact 2", Hashtags = ["#test2"] }
            ],
            LinkTweets = [new TweetLinkResult
            {
                Text = "Check this out",
                Hashtags = ["#cars"],
                PostUrl = "https://example.com/post",
                PostTitle = "Test Post"
            }],
            EnabledPlatforms = ["Twitter/X"]
        };

        var result = await activity.Run(input);

        result.Should().BeTrue();
        // 2 facts + 1 link = 3 items for 1 platform
        capturedItems.Should().HaveCount(3);
        capturedItems.Count(i => i.Type == "fact").Should().Be(2);
        capturedItems.Count(i => i.Type == "link").Should().Be(1);
        capturedItems.All(i => i.Platform == "Twitter/X").Should().BeTrue();
    }

    [Fact]
    public async Task StoreSocialQueue_MultiplePlatforms_DuplicatesItems()
    {
        var queueStore = new Mock<ISocialMediaQueueStore>();
        var capturedItems = new List<SocialMediaQueueItem>();
        queueStore.Setup(s => s.AddItemsAsync(It.IsAny<IEnumerable<SocialMediaQueueItem>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<SocialMediaQueueItem>, CancellationToken>((items, _) => capturedItems.AddRange(items))
            .Returns(Task.CompletedTask);

        var activity = new StoreSocialMediaQueueActivity(
            queueStore.Object,
            Mock.Of<ILogger<StoreSocialMediaQueueActivity>>());

        var input = new StoreSocialQueueInput
        {
            Facts = [new TweetFactResult { Text = "Fact 1", Hashtags = ["#test"] }],
            LinkTweets = [],
            EnabledPlatforms = ["Twitter/X", "Facebook"]
        };

        await activity.Run(input);

        // 1 fact × 2 platforms = 2 items
        capturedItems.Should().HaveCount(2);
        capturedItems.Select(i => i.Platform).Distinct().Should().HaveCount(2);
    }

    #endregion

    #region ExecuteScheduledPostActivity

    [Fact]
    public async Task ExecuteScheduledPost_FactItem_PostsAndDeletes_NoSocialCountIncrement()
    {
        var factStore = new Mock<IFactKeywordStore>();
        var queueStore = new Mock<ISocialMediaQueueStore>();

        var mockService = new Mock<ISocialMediaService>();
        mockService.Setup(s => s.PlatformName).Returns("Twitter/X");
        mockService.Setup(s => s.IsEnabled).Returns(true);

        var publisher = new CarFacts.Functions.Services.SocialMediaPublisher(
            new[] { mockService.Object },
            Mock.Of<ILogger<CarFacts.Functions.Services.SocialMediaPublisher>>());

        var activity = new ExecuteScheduledPostActivity(
            publisher,
            queueStore.Object,
            factStore.Object,
            Mock.Of<ILogger<ExecuteScheduledPostActivity>>());

        var input = new ScheduledPostInput
        {
            ItemId = "test-id",
            Platform = "Twitter/X",
            Content = "Cool car fact #CarHistory",
            Type = "fact",
            ScheduledAtUtc = DateTime.UtcNow
        };

        var result = await activity.Run(input);

        result.Should().BeTrue();
        mockService.Verify(s => s.PostRawAsync("Cool car fact #CarHistory", It.IsAny<CancellationToken>()), Times.Once);
        queueStore.Verify(s => s.DeleteItemAsync("test-id", "Twitter/X", It.IsAny<CancellationToken>()), Times.Once);
        factStore.Verify(s => s.IncrementSocialCountsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteScheduledPost_LinkItem_PostsDeletesAndIncrementsCounts()
    {
        var factStore = new Mock<IFactKeywordStore>();
        var queueStore = new Mock<ISocialMediaQueueStore>();

        var mockService = new Mock<ISocialMediaService>();
        mockService.Setup(s => s.PlatformName).Returns("Twitter/X");
        mockService.Setup(s => s.IsEnabled).Returns(true);

        var publisher = new CarFacts.Functions.Services.SocialMediaPublisher(
            new[] { mockService.Object },
            Mock.Of<ILogger<CarFacts.Functions.Services.SocialMediaPublisher>>());

        var activity = new ExecuteScheduledPostActivity(
            publisher,
            queueStore.Object,
            factStore.Object,
            Mock.Of<ILogger<ExecuteScheduledPostActivity>>());

        var input = new ScheduledPostInput
        {
            ItemId = "link-id",
            Platform = "Twitter/X",
            Content = "Check out this post https://example.com",
            Type = "link",
            PostUrl = "https://example.com/my-post",
            ScheduledAtUtc = DateTime.UtcNow
        };

        var result = await activity.Run(input);

        result.Should().BeTrue();
        mockService.Verify(s => s.PostRawAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        queueStore.Verify(s => s.DeleteItemAsync("link-id", "Twitter/X", It.IsAny<CancellationToken>()), Times.Once);
        factStore.Verify(s => s.IncrementSocialCountsAsync("https://example.com/my-post", "Twitter/X", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetEnabledPlatformsActivity

    [Fact]
    public async Task GetEnabledPlatforms_ReturnsEnabledPlatformNames()
    {
        var twitter = new Mock<ISocialMediaService>();
        twitter.Setup(s => s.PlatformName).Returns("Twitter/X");
        twitter.Setup(s => s.IsEnabled).Returns(true);

        var facebook = new Mock<ISocialMediaService>();
        facebook.Setup(s => s.PlatformName).Returns("Facebook");
        facebook.Setup(s => s.IsEnabled).Returns(false);

        var publisher = new CarFacts.Functions.Services.SocialMediaPublisher(
            new[] { twitter.Object, facebook.Object },
            Mock.Of<ILogger<CarFacts.Functions.Services.SocialMediaPublisher>>());

        var activity = new GetEnabledPlatformsActivity(
            publisher,
            Mock.Of<ILogger<GetEnabledPlatformsActivity>>());

        var result = await activity.Run("check");

        result.Should().HaveCount(1);
        result[0].Should().Be("Twitter/X");
    }

    #endregion
}
