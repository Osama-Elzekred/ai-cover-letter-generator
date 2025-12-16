namespace CoverLetter.Application.Common.Interfaces;

/// <summary>
/// Interface for LLM (Large Language Model) service.
/// Defined in Application layer, implemented in Infrastructure.
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Generates text based on the provided prompt and optional parameters.
    /// </summary>
    /// <param name="prompt">The user prompt/instruction</param>
    /// <param name="options">Optional generation parameters (system message, temperature, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<LlmResponse> GenerateAsync(
        string prompt,
        LlmGenerationOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for customizing LLM generation behavior.
/// </summary>
public sealed record LlmGenerationOptions(
    string? SystemMessage = null,
    double? Temperature = null,
    int? MaxTokens = null,
    string? ApiKey = null  // Optional: User's API key for BYOK pattern
);

/// <summary>
/// Response from the LLM service.
/// </summary>
public sealed record LlmResponse(
    string Content,
    string Model,
    int PromptTokens,
    int CompletionTokens
);
