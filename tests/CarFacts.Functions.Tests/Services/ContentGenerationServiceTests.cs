using System.Net;
using System.Text.Json;
using CarFacts.Functions.Configuration;
using CarFacts.Functions.Models;
using CarFacts.Functions.Services;
using CarFacts.Functions.Services.Interfaces;
using CarFacts.Functions.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;

namespace CarFacts.Functions.Tests.Services;

public class ContentGenerationServiceTests
{
    private readonly Mock<IChatCompletionService> _chatService;
    private readonly ContentGenerationService _sut;

    public ContentGenerationServiceTests()
    {
        _chatService = new Mock<IChatCompletionService>();

        _sut = new ContentGenerationService(
            _chatService.Object,
            Mock.Of<ILogger<ContentGenerationService>>());
    }

    private void SetupChatResponse(string content)
    {
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, content);
        _chatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });
    }

    [Fact]
    public async Task GenerateFactsAsync_WithValidResponse_ReturnsParsedFacts()
    {
        var expected = TestDataBuilder.CreateValidResponse();
        var json = JsonSerializer.Serialize(expected);
        SetupChatResponse(json);

        var result = await _sut.GenerateFactsAsync("March 21");

        result.MainTitle.Should().Be(expected.MainTitle);
        result.Facts.Should().HaveCount(5);
        result.Keywords.Should().NotBeEmpty();
        result.MetaDescription.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenerateFactsAsync_WithMarkdownWrappedJson_ParsesSuccessfully()
    {
        var expected = TestDataBuilder.CreateValidResponse();
        var json = JsonSerializer.Serialize(expected);
        SetupChatResponse($"```json\n{json}\n```");

        var result = await _sut.GenerateFactsAsync("March 21");

        result.Facts.Should().HaveCount(5);
    }

    [Fact]
    public async Task GenerateFactsAsync_WithWrongFactCount_ThrowsException()
    {
        var badResponse = TestDataBuilder.CreateValidResponse(factCount: 3);
        SetupChatResponse(JsonSerializer.Serialize(badResponse));

        var act = () => _sut.GenerateFactsAsync("March 21");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Expected 5 facts*");
    }

    [Fact]
    public async Task GenerateFactsAsync_WithMissingTitle_ThrowsException()
    {
        var badResponse = TestDataBuilder.CreateValidResponse();
        badResponse.MainTitle = "";
        SetupChatResponse(JsonSerializer.Serialize(badResponse));

        var act = () => _sut.GenerateFactsAsync("March 21");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing main_title*");
    }

    [Fact]
    public async Task GenerateFactsAsync_CallsChatService()
    {
        var expected = TestDataBuilder.CreateValidResponse();
        SetupChatResponse(JsonSerializer.Serialize(expected));

        await _sut.GenerateFactsAsync("March 21");

        _chatService.Verify(
            s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
