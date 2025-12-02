namespace CoverLetter.Application.Common.Interfaces;

/// <summary>
/// Interface for LLM (Large Language Model) service.
/// Defined in Application layer, implemented in Infrastructure.
/// </summary>
public interface ILlmService
{
  /// <summary>
  /// Generates text based on the provided prompt.
  /// </summary>
  Task<LlmResponse> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}

/// <summary>
/// Response from the LLM service.
/// </summary>
public sealed record LlmResponse(
    string Content,
    string Model,
    int PromptTokens,
    int CompletionTokens
);
