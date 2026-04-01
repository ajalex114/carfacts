using System.Net;
using CarFacts.Functions.Configuration;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services;
using CarFacts.Functions.Services.Interfaces;
using CarFacts.Functions.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CarFacts.Functions.Tests.Services;

public class WordPressServiceTests
{
    private readonly FakeHttpMessageHandler _handler;
    private readonly Mock<ISecretProvider> _secretProvider;
    private readonly WordPressService _sut;

    public WordPressServiceTests()
    {
        _handler = new FakeHttpMessageHandler();
        _secretProvider = new Mock<ISecretProvider>();
        _secretProvider
            .Setup(s => s.GetSecretAsync(SecretNames.WordPressOAuthToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-oauth-token");

        var settings = Options.Create(new WordPressSettings
        {
            SiteId = "mysite.wordpress.com",
            PostStatus = "publish"
        });

        _sut = new WordPressService(
            new HttpClient(_handler),
            settings,
            _secretProvider.Object,
            Mock.Of<ILogger<WordPressService>>());
    }

    [Fact]
    public async Task UploadImagesAsync_UploadsAllImages()
    {
        var images = TestDataBuilder.CreateGeneratedImages(3);
        var facts = TestDataBuilder.CreateValidResponse(3).Facts;

        for (int i = 0; i < 3; i++)
            _handler.EnqueueResponse(HttpStatusCode.Created, TestDataBuilder.CreateWordPressMediaResponseJson(100 + i, i));

        var result = await _sut.UploadImagesAsync(images, facts);

        result.Should().HaveCount(3);
        _handler.SentRequests.Should().HaveCount(3);
    }

    [Fact]
    public async Task UploadImagesAsync_SendsToWordPressComApi()
    {
        var images = TestDataBuilder.CreateGeneratedImages(1);
        var facts = TestDataBuilder.CreateValidResponse(1).Facts;
        _handler.EnqueueResponse(HttpStatusCode.Created, TestDataBuilder.CreateWordPressMediaResponseJson(100, 0));

        await _sut.UploadImagesAsync(images, facts);

        _handler.SentRequests[0].RequestUri!.ToString()
            .Should().Be("https://public-api.wordpress.com/rest/v1.1/sites/mysite.wordpress.com/media/new");
    }

    [Fact]
    public async Task UploadImagesAsync_UsesBearerAuth()
    {
        var images = TestDataBuilder.CreateGeneratedImages(1);
        var facts = TestDataBuilder.CreateValidResponse(1).Facts;
        _handler.EnqueueResponse(HttpStatusCode.Created, TestDataBuilder.CreateWordPressMediaResponseJson(100, 0));

        await _sut.UploadImagesAsync(images, facts);

        var request = _handler.SentRequests[0];
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("test-oauth-token");
    }

    [Fact]
    public async Task UploadImagesAsync_ReturnsCorrectMediaIds()
    {
        var images = TestDataBuilder.CreateGeneratedImages(2);
        var facts = TestDataBuilder.CreateValidResponse(2).Facts;
        _handler.EnqueueResponse(HttpStatusCode.Created, TestDataBuilder.CreateWordPressMediaResponseJson(101, 0));
        _handler.EnqueueResponse(HttpStatusCode.Created, TestDataBuilder.CreateWordPressMediaResponseJson(102, 1));

        var result = await _sut.UploadImagesAsync(images, facts);

        result[0].MediaId.Should().Be(101);
        result[1].MediaId.Should().Be(102);
    }

    [Fact]
    public async Task CreatePostAsync_ReturnsPostResult()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, TestDataBuilder.CreateWordPressPostResponseJson());

        var result = await _sut.CreatePostAsync(
            "Test Title", "<p>Content</p>", "Excerpt", 100, "keyword1, keyword2", "Meta desc");

        result.PostId.Should().Be(42);
        result.PostUrl.Should().Be("https://example.com/shocking-car-moments-march-21/");
        result.Title.Should().Contain("Shocking Car Moments");
    }

    [Fact]
    public async Task CreatePostAsync_SendsToWordPressComApi()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, TestDataBuilder.CreateWordPressPostResponseJson());

        await _sut.CreatePostAsync("Title", "<p>Content</p>", "Excerpt", 100, "kw", "Meta");

        _handler.SentRequests[0].RequestUri!.ToString()
            .Should().Be("https://public-api.wordpress.com/rest/v1.1/sites/mysite.wordpress.com/posts/new");
    }

    [Fact]
    public async Task CreatePostAsync_WhenApiFails_ThrowsHttpRequestException()
    {
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized, "{}");

        var act = () => _sut.CreatePostAsync("Title", "<p>Content</p>", "Excerpt", 100, "kw", "Meta");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CreatePostAsync_RetrievesOAuthTokenFromSecretProvider()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, TestDataBuilder.CreateWordPressPostResponseJson());

        await _sut.CreatePostAsync("Title", "<p>Content</p>", "Excerpt", 100, "kw", "Meta");

        _secretProvider.Verify(s => s.GetSecretAsync(SecretNames.WordPressOAuthToken, It.IsAny<CancellationToken>()), Times.Once);
    }
}
