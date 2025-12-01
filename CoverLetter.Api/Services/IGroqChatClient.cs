using CoverLetter.Api.Models;

namespace CoverLetter.Api.Services;

/// <summary>
/// Interface for Groq chat completion client.
/// </summary>
public interface IGroqChatClient
{
  /// <summary>
  /// Sends a chat completion request to Groq API.
  /// </summary>
  Task<GroqChatResponse> ChatCompletionAsync(
      List<GroqMessage> messages,
      CancellationToken cancellationToken = default);
}
