using CarFacts.Functions.Models;
using CarFacts.Functions.Services;
using CarFacts.Functions.Services.Interfaces;
using CarFacts.Functions.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CarFacts.Functions.Tests.Services;

public class FallbackImageGenerationServiceTests
{
    private readonly Mock<IImageGenerationService> _primaryProvider;
    private readonly Mock<IImageGenerationService> _secondaryProvider;
    private readonly FallbackImageGenerationService _sut;

    public FallbackImageGenerationServiceTests()
    {
        _primaryProvider = new Mock<IImageGenerationService>();
        _secondaryProvider = new Mock<IImageGenerationService>();

        _sut = new FallbackImageGenerationService(
            new[] { _primaryProvider.Object, _secondaryProvider.Object },
            Mock.Of<ILogger<FallbackImageGenerationService>>());
    }

    private static List<CarFact> CreateFacts(int count = 1) =>
        TestDataBuilder.CreateValidResponse().Facts.Take(count).ToList();

    private static List<GeneratedImage> CreateImages(int count = 1) =>
        Enumerable.Range(0, count).Select(i => new GeneratedImage
        {
            FactIndex = i,
            ImageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 },
            FileName = $"test-{i}.png"
        }).ToList();

    [Fact]
    public async Task PrimarySucceeds_ReturnsPrimaryImages()
    {
        var facts = CreateFacts();
        var images = CreateImages();
        _primaryProvider.Setup(p => p.GenerateImagesAsync(facts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(images);

        var result = await _sut.GenerateImagesAsync(facts);

        result.Should().BeSameAs(images);
        _secondaryProvider.Verify(
            p => p.GenerateImagesAsync(It.IsAny<List<CarFact>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PrimaryFails_FallsBackToSecondary()
    {
        var facts = CreateFacts();
        var images = CreateImages();
        _primaryProvider.Setup(p => p.GenerateImagesAsync(facts, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("429 Too Many Requests"));
        _secondaryProvider.Setup(p => p.GenerateImagesAsync(facts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(images);

        var result = await _sut.GenerateImagesAsync(facts);

        result.Should().BeSameAs(images);
    }

    [Fact]
    public async Task BothFail_ReturnsEmptyList()
    {
        var facts = CreateFacts();
        _primaryProvider.Setup(p => p.GenerateImagesAsync(facts, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("429"));
        _secondaryProvider.Setup(p => p.GenerateImagesAsync(facts, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("500"));

        var result = await _sut.GenerateImagesAsync(facts);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task PrimaryReturnsEmpty_TriesSecondary()
    {
        var facts = CreateFacts();
        var images = CreateImages();
        _primaryProvider.Setup(p => p.GenerateImagesAsync(facts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GeneratedImage>());
        _secondaryProvider.Setup(p => p.GenerateImagesAsync(facts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(images);

        var result = await _sut.GenerateImagesAsync(facts);

        result.Should().BeSameAs(images);
    }

    [Fact]
    public async Task CancellationToken_PropagatesCancellation()
    {
        var facts = CreateFacts();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _primaryProvider.Setup(p => p.GenerateImagesAsync(facts, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _sut.GenerateImagesAsync(facts, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
