using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.UseCases.GenerateCoverLetter;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using CoverLetter.Domain.Common;

namespace CoverLetter.Application.Tests.UseCases.GenerateCoverLetter;

/// <summary>
/// Unit tests for GenerateCoverLetterHandler.
/// Following TDD - these tests define the expected behavior.
/// </summary>
public class GenerateCoverLetterHandlerTests
{
  private readonly ILlmService _llmService;
  private readonly ICvRepository _cvRepository;
  private readonly IPromptRegistry _promptRegistry;
  private readonly ICustomPromptService _customPromptService;
  private readonly IUserContext _userContext;
  private readonly ILogger<GenerateCoverLetterHandler> _logger;
  private readonly GenerateCoverLetterHandler _handler;

  public GenerateCoverLetterHandlerTests()
  {
    _llmService = Substitute.For<ILlmService>();
    _cvRepository = Substitute.For<ICvRepository>();
    _promptRegistry = Substitute.For<IPromptRegistry>();
    _customPromptService = Substitute.For<ICustomPromptService>();
    _userContext = Substitute.For<IUserContext>();
    _logger = Substitute.For<ILogger<GenerateCoverLetterHandler>>();

    // Setup default user context (no saved API key)
    _userContext.UserId.Returns((string?)null);
    _userContext.GetUserApiKey().Returns((string?)null);

    // Default prompt registry behavior
    _promptRegistry.GetPrompt(PromptType.CoverLetter, Arg.Any<Dictionary<string, string>>())
      .Returns(Result.Success("Default prompt: Job: {JobDescription} CV: {CvText}"));

    // No saved custom prompt by default
    _customPromptService.GetUserPromptAsync(PromptType.CoverLetter, Arg.Any<CancellationToken>())
      .Returns((string?)null);

    _handler = new GenerateCoverLetterHandler(_llmService, _cvRepository, _userContext, _promptRegistry, _customPromptService, _logger);
  }

  [Fact]
  public async Task Handle_ValidRequest_ReturnsCoverLetter()
  {
    // Arrange
    var command = new GenerateCoverLetterCommand(
        JobDescription: "We need a .NET developer",
        CvText: "I have 5 years of experience in .NET"
    );

    var expectedContent = "Dear Hiring Manager, I am excited to apply...";
    _llmService.GenerateAsync(
        Arg.Any<string>(),
        Arg.Any<LlmGenerationOptions>(),
        Arg.Any<CancellationToken>())
        .Returns(new LlmResponse(
            Content: expectedContent,
            Model: "llama-3.3-70b-versatile",
            PromptTokens: 100,
            CompletionTokens: 200
        ));

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().NotBeNull();
    result.Value.CoverLetter.Should().Be(expectedContent);
    result.Value.Model.Should().Be("llama-3.3-70b-versatile");
    result.Value.PromptTokens.Should().Be(100);
    result.Value.CompletionTokens.Should().Be(200);
  }

  [Fact]
  public async Task Handle_LlmServiceFails_ReturnsFailure()
  {
    // Arrange
    var command = new GenerateCoverLetterCommand(
        JobDescription: "We need a .NET developer",
        CvText: "I have 5 years of experience"
    );

    _llmService.GenerateAsync(
        Arg.Any<string>(),
        Arg.Any<LlmGenerationOptions>(),
        Arg.Any<CancellationToken>())
        .Returns<LlmResponse>(_ => throw new HttpRequestException("API is down"));

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Should().Contain("API is down");
  }

  [Fact]
  public async Task Handle_CustomPromptTemplate_UsesCustomTemplate()
  {
    // Arrange
    var customTemplate = "Custom template: Job: {0}, CV: {1}";
    var command = new GenerateCoverLetterCommand(
        JobDescription: "Developer role",
        CvText: "My CV",
        CustomPromptTemplate: customTemplate
    );

    string? capturedPrompt = null;
    _llmService.GenerateAsync(
        Arg.Do<string>(p => capturedPrompt = p),
        Arg.Any<LlmGenerationOptions>(),
        Arg.Any<CancellationToken>())
        .Returns(new LlmResponse("Generated content", "model", 50, 100));

    // Act
    await _handler.Handle(command, CancellationToken.None);

    // Assert
    capturedPrompt.Should().NotBeNull();
    capturedPrompt!.Should().Contain("Default prompt");
    capturedPrompt.Should().Contain("ADDITIONAL INSTRUCTIONS:");
    capturedPrompt.Should().Contain(customTemplate);
  }

  [Fact]
  public async Task Handle_ValidRequest_CallsLlmServiceOnce()
  {
    // Arrange
    var command = new GenerateCoverLetterCommand(
        JobDescription: "Job description",
        CvText: "CV text"
    );

    _llmService.GenerateAsync(
        Arg.Any<string>(),
        Arg.Any<LlmGenerationOptions>(),
        Arg.Any<CancellationToken>())
        .Returns(new LlmResponse("Content", "model", 50, 100));

    // Act
    await _handler.Handle(command, CancellationToken.None);

    // Assert
    await _llmService.Received(1).GenerateAsync(
        Arg.Any<string>(),
        Arg.Any<LlmGenerationOptions>(),
        Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_CustomPrompt_AppendMode_AppendsToDefault()
  {
    // Arrange
    var command = new GenerateCoverLetterCommand(
        JobDescription: "Senior .NET Developer",
        CvText: "Experienced in microservices",
        CustomPromptTemplate: "Please emphasize system design and distributed systems.",
        PromptMode: PromptMode.Append
    );

    string? capturedPrompt = null;
    _llmService.GenerateAsync(
        Arg.Do<string>(p => capturedPrompt = p),
        Arg.Any<LlmGenerationOptions>(),
        Arg.Any<CancellationToken>())
        .Returns(new LlmResponse("Content", "model", 10, 20));

    // Act
    await _handler.Handle(command, CancellationToken.None);

    // Assert
    capturedPrompt.Should().NotBeNull();
    capturedPrompt!.Should().Contain("Default prompt");
    capturedPrompt.Should().Contain("ADDITIONAL INSTRUCTIONS:");
    capturedPrompt.Should().Contain("system design and distributed systems");
  }

  [Fact]
  public async Task Handle_CustomPrompt_OverrideMode_UsesCustomOnly()
  {
    // Arrange
    var custom = "Cover letter for {JobDescription} using CV {CvText}";
    var command = new GenerateCoverLetterCommand(
        JobDescription: "Backend Engineer",
        CvText: "5+ years in C#",
        CustomPromptTemplate: custom,
        PromptMode: PromptMode.Override
    );

    string? capturedPrompt = null;
    _llmService.GenerateAsync(
        Arg.Do<string>(p => capturedPrompt = p),
        Arg.Any<LlmGenerationOptions>(),
        Arg.Any<CancellationToken>())
        .Returns(new LlmResponse("Content", "model", 10, 20));

    // Act
    await _handler.Handle(command, CancellationToken.None);

    // Assert
    capturedPrompt.Should().NotBeNull();
    capturedPrompt!.Should().Contain("Cover letter for Backend Engineer using CV 5+ years in C#");
    capturedPrompt.Should().NotContain("ADDITIONAL INSTRUCTIONS:");
    capturedPrompt.Should().NotContain("Default prompt");
  }
}
