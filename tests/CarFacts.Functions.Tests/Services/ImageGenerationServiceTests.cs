using System.Net;
using CarFacts.Functions.Configuration;
using CarFacts.Functions.Services;
using CarFacts.Functions.Services.Interfaces;
using CarFacts.Functions.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CarFacts.Functions.Tests.Services;

public class ImageGenerationServiceTests
{
    private readonly FakeHttpMessageHandler _handler;
    private readonly Mock<ISecretProvider> _secretProvider;
    private readonly ImageGenerationService _sut;

    public ImageGenerationServiceTests()
    {
        _handler = new FakeHttpMessageHandler();
        _secretProvider = new Mock<ISecretProvider>();
        _secretProvider
            .Setup(s => s.GetSecretAsync(SecretNames.StabilityAIApiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-stability-key");

        var settings = Options.Create(new StabilityAISettings
        {
            BaseUrl = "https://api.stability.ai",
            Model = "stable-diffusion-xl-1024-v1-0",
            Width = 1024,
            Height = 1024,
            Steps = 30,
            CfgScale = 7
        });

        _sut = new ImageGenerationService(
            new HttpClient(_handler),
            settings,
            _secretProvider.Object,
            Mock.Of<ILogger<ImageGenerationService>>());
    }

    [Fact]
    public async Task GenerateImagesAsync_ReturnsOneImagePerFact()
    {
        var facts = TestDataBuilder.CreateValidResponse().Facts;
        foreach (var _ in facts)
            _handler.EnqueueResponse(HttpStatusCode.OK, TestDataBuilder.CreateStabilityAIResponseJson());

        var result = await _sut.GenerateImagesAsync(facts);

        result.Should().HaveCount(5);
        result.Select(r => r.FactIndex).Should().BeEquivalentTo([0, 1, 2, 3, 4]);
    }

    [Fact]
    public async Task GenerateImagesAsync_SetsCorrectFileNames()
    {
        var facts = TestDataBuilder.CreateValidResponse().Facts;
        foreach (var _ in facts)
            _handler.EnqueueResponse(HttpStatusCode.OK, TestDataBuilder.CreateStabilityAIResponseJson());

        var result = await _sut.GenerateImagesAsync(facts);

        result.Should().AllSatisfy(img =>
        {
            img.FileName.Should().StartWith("car-fact-");
            img.FileName.Should().EndWith(".png");
        });
    }

    [Fact]
    public async Task GenerateImagesAsync_DecodesBase64ImageData()
    {
        var facts = TestDataBuilder.CreateValidResponse().Facts.Take(1).ToList();
        _handler.EnqueueResponse(HttpStatusCode.OK, TestDataBuilder.CreateStabilityAIResponseJson());

        var result = await _sut.GenerateImagesAsync(facts);

        result[0].ImageData.Should().NotBeEmpty();
        result[0].ImageData[0].Should().Be(0x89); // PNG magic byte
    }

    [Fact]
    public async Task GenerateImagesAsync_SendsBearerAuthHeader()
    {
        var facts = TestDataBuilder.CreateValidResponse().Facts.Take(1).ToList();
        _handler.EnqueueResponse(HttpStatusCode.OK, TestDataBuilder.CreateStabilityAIResponseJson());

        await _sut.GenerateImagesAsync(facts);

        var request = _handler.SentRequests[0];
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("test-stability-key");
    }

    [Fact]
    public async Task GenerateImagesAsync_CallsCorrectEndpoint()
    {
        var facts = TestDataBuilder.CreateValidResponse().Facts.Take(1).ToList();
        _handler.EnqueueResponse(HttpStatusCode.OK, TestDataBuilder.CreateStabilityAIResponseJson());

        await _sut.GenerateImagesAsync(facts);

        var request = _handler.SentRequests[0];
        request.RequestUri!.ToString().Should().Contain("stable-diffusion-xl-1024-v1-0/text-to-image");
    }

    [Fact]
    public async Task GenerateImagesAsync_WhenRateLimitedAndRetriesExhausted_ThrowsHttpRequestException()
    {
        var facts = TestDataBuilder.CreateValidResponse().Facts.Take(1).ToList();
        // 1 initial + 3 retries = 4 responses needed to exhaust
        for (int i = 0; i < 4; i++)
            _handler.EnqueueResponse(HttpStatusCode.TooManyRequests, "{}");

        var act = () => _sut.GenerateImagesAsync(facts);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GenerateImagesAsync_WhenRateLimitedThenSucceeds_ReturnsImage()
    {
        var facts = TestDataBuilder.CreateValidResponse().Facts.Take(1).ToList();
        // First attempt: 429, second attempt: success
        _handler.EnqueueResponse(HttpStatusCode.TooManyRequests, "{}");
        _handler.EnqueueResponse(HttpStatusCode.OK, TestDataBuilder.CreateStabilityAIResponseJson());

        var result = await _sut.GenerateImagesAsync(facts);

        result.Should().HaveCount(1);
        _handler.SentRequests.Should().HaveCount(2);
    }

    [Fact]
    public async Task GenerateImagesAsync_RetrievesKeyFromSecretProvider()
    {
        var facts = TestDataBuilder.CreateValidResponse().Facts.Take(1).ToList();
        _handler.EnqueueResponse(HttpStatusCode.OK, TestDataBuilder.CreateStabilityAIResponseJson());

        await _sut.GenerateImagesAsync(facts);

        _secretProvider.Verify(
            s => s.GetSecretAsync(SecretNames.StabilityAIApiKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
