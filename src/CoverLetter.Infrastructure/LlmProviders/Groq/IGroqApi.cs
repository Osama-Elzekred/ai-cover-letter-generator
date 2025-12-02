using Refit;

namespace CoverLetter.Infrastructure.LlmProviders.Groq;

/// <summary>
/// Refit interface for Groq API.
/// Refit generates the HTTP client implementation at compile time.
/// </summary>
public interface IGroqApi
{
  /// <summary>
  /// Sends a chat completion request to Groq API.
  /// </summary>
  [Post("/chat/completions")]
  Task<GroqChatResponse> ChatCompletionAsync(
      [Body] GroqChatRequest request,
      CancellationToken cancellationToken = default);
}
