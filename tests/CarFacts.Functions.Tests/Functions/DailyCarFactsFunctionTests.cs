using CarFacts.Functions.Configuration;
using CarFacts.Functions.Functions;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services;
using CarFacts.Functions.Services.Interfaces;
using CarFacts.Functions.Tests.Helpers;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CarFacts.Functions.Tests.Functions;

public class DailyCarFactsFunctionTests
{
    private readonly Mock<IContentGenerationService> _contentGen;
    private readonly Mock<IImageGenerationService> _imageGen;
    private readonly Mock<IWordPressService> _wordPress;
    private readonly Mock<IContentFormatterService> _formatter;
    private readonly Mock<ILogger<DailyCarFactsFunction>> _logger;
    private readonly DailyCarFactsFunction _sut;

    public DailyCarFactsFunctionTests()
    {
        _contentGen = new Mock<IContentGenerationService>();
        _imageGen = new Mock<IImageGenerationService>();
        _wordPress = new Mock<IWordPressService>();
        _formatter = new Mock<IContentFormatterService>();
        _logger = new Mock<ILogger<DailyCarFactsFunction>>();

        var wpSettings = Options.Create(new WordPressSettings
        {
            SiteId = "test.wordpress.com",
            PostStatus = "draft",
            SkipImages = false
        });

        var socialPublisher = new SocialMediaPublisher(
            Enumerable.Empty<ISocialMediaService>(),
            new Mock<ILogger<SocialMediaPublisher>>().Object);

        _sut = new DailyCarFactsFunction(
            _contentGen.Object,
            _imageGen.Object,
            _wordPress.Object,
            _formatter.Object,
            socialPublisher,
            wpSettings,
            _logger.Object);

        SetupDefaults();
    }

    private void SetupDefaults()
    {
        var response = TestDataBuilder.CreateValidResponse();
        var images = TestDataBuilder.CreateGeneratedImages();
        var media = TestDataBuilder.CreateUploadedMedia();
        var postResult = TestDataBuilder.CreatePostResult();

        _contentGen
            .Setup(s => s.GenerateFactsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        _imageGen
            .Setup(s => s.GenerateImagesAsync(It.IsAny<List<CarFact>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(images);

        _wordPress
            .Setup(s => s.UploadImagesAsync(It.IsAny<List<GeneratedImage>>(), It.IsAny<List<CarFact>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);

        _formatter
            .Setup(s => s.FormatPostHtml(It.IsAny<CarFactsResponse>(), It.IsAny<List<UploadedMedia>>(), It.IsAny<string>()))
            .Returns("<p>HTML content</p>");

        _wordPress
            .Setup(s => s.CreatePostAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(postResult);
    }

    [Fact]
    public async Task RunAsync_CallsAllServicesInOrder()
    {
        var callOrder = new List<string>();
        _contentGen.Setup(s => s.GenerateFactsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("content"))
            .ReturnsAsync(TestDataBuilder.CreateValidResponse());
        _imageGen.Setup(s => s.GenerateImagesAsync(It.IsAny<List<CarFact>>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("images"))
            .ReturnsAsync(TestDataBuilder.CreateGeneratedImages());
        _wordPress.Setup(s => s.UploadImagesAsync(It.IsAny<List<GeneratedImage>>(), It.IsAny<List<CarFact>>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("upload"))
            .ReturnsAsync(TestDataBuilder.CreateUploadedMedia());
        _wordPress.Setup(s => s.CreatePostAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("publish"))
            .ReturnsAsync(TestDataBuilder.CreatePostResult());

        await _sut.RunAsync(CreateTimerInfo(), CancellationToken.None);

        callOrder.Should().ContainInOrder("content", "images", "upload", "publish");
    }

    [Fact]
    public async Task RunAsync_PassesFactsFromContentGenToImageGen()
    {
        var expectedResponse = TestDataBuilder.CreateValidResponse();
        _contentGen.Setup(s => s.GenerateFactsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        await _sut.RunAsync(CreateTimerInfo(), CancellationToken.None);

        _imageGen.Verify(s => s.GenerateImagesAsync(expectedResponse.Facts, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_PassesFormattedHtmlToWordPress()
    {
        _formatter.Setup(s => s.FormatPostHtml(It.IsAny<CarFactsResponse>(), It.IsAny<List<UploadedMedia>>(), It.IsAny<string>()))
            .Returns("<div>Formatted</div>");

        await _sut.RunAsync(CreateTimerInfo(), CancellationToken.None);

        _wordPress.Verify(s => s.CreatePostAsync(
            It.IsAny<string>(),
            "<div>Formatted</div>",
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_UsesFeaturedMediaFromFirstUpload()
    {
        var media = TestDataBuilder.CreateUploadedMedia();
        _wordPress.Setup(s => s.UploadImagesAsync(It.IsAny<List<GeneratedImage>>(), It.IsAny<List<CarFact>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);

        await _sut.RunAsync(CreateTimerInfo(), CancellationToken.None);

        _wordPress.Verify(s => s.CreatePostAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            media[0].MediaId,
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenContentGenFails_DoesNotCallSubsequentServices()
    {
        _contentGen.Setup(s => s.GenerateFactsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API error"));

        var act = () => _sut.RunAsync(CreateTimerInfo(), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        _imageGen.Verify(s => s.GenerateImagesAsync(It.IsAny<List<CarFact>>(), It.IsAny<CancellationToken>()), Times.Never);
        _wordPress.Verify(s => s.CreatePostAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenImageGenFails_DoesNotPublish()
    {
        _imageGen.Setup(s => s.GenerateImagesAsync(It.IsAny<List<CarFact>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Rate limited"));

        var act = () => _sut.RunAsync(CreateTimerInfo(), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        _wordPress.Verify(s => s.CreatePostAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_PassesSeoKeywordsAsCommaDelimited()
    {
        var response = TestDataBuilder.CreateValidResponse();
        _contentGen.Setup(s => s.GenerateFactsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        await _sut.RunAsync(CreateTimerInfo(), CancellationToken.None);

        var expectedKeywords = string.Join(", ", response.Keywords);
        _wordPress.Verify(s => s.CreatePostAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(),
            expectedKeywords,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static TimerInfo CreateTimerInfo()
    {
        return new TimerInfo();
    }
}
