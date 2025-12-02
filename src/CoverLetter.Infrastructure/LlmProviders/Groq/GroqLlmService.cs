using CoverLetter.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoverLetter.Infrastructure.LlmProviders.Groq;

/// <summary>
/// Groq implementation of ILlmService using Refit.
/// </summary>
public sealed class GroqLlmService : ILlmService
{
  private readonly IGroqApi _groqApi;
  private readonly GroqSettings _settings;
  private readonly ILogger<GroqLlmService> _logger;

  public GroqLlmService(
      IGroqApi groqApi,
      IOptions<GroqSettings> settings,
      ILogger<GroqLlmService> logger)
  {
    _groqApi = groqApi;
    _settings = settings.Value;
    _logger = logger;
  }

  public async Task<LlmResponse> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
  {
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    var request = new GroqChatRequest(
        Model: _settings.Model,
        Messages:
        [
            new GroqMessage("system", "You are a professional cover letter writer. Respond only with the cover letter text, no additional commentary."),
                new GroqMessage("user", prompt)
        ],
        Temperature: _settings.Temperature,
        MaxTokens: _settings.MaxTokens
    );

    var response = await _groqApi.ChatCompletionAsync(request, cancellationToken);
    stopwatch.Stop();

    var content = response.Choices.FirstOrDefault()?.Message.Content
        ?? throw new InvalidOperationException("No content returned from Groq API.");

    _logger.LogInformation(
        "Groq API responded in {ElapsedMs}ms - Tokens: {PromptTokens}→{CompletionTokens}",
        stopwatch.ElapsedMilliseconds,
        response.Usage.PromptTokens,
        response.Usage.CompletionTokens);

    return new LlmResponse(
        Content: content,
        Model: response.Model,
        PromptTokens: response.Usage.PromptTokens,
        CompletionTokens: response.Usage.CompletionTokens
    );
  }
}
