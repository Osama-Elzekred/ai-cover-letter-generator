namespace CoverLetter.Infrastructure.LlmProviders.Groq;

/// <summary>
/// Configuration settings for Groq API.
/// </summary>
public sealed class GroqSettings
{
  public const string SectionName = "Groq";

  /// <summary>
  /// Groq API key.
  /// </summary>
  public required string ApiKey { get; init; }

  /// <summary>
  /// Base URL for Groq API.
  /// </summary>
  public string BaseUrl { get; init; } = "https://api.groq.com/openai/v1";

  /// <summary>
  /// Model to use for chat completions.
  /// </summary>
  public string Model { get; init; } = "llama-3.3-70b-versatile";

  /// <summary>
  /// Temperature for generation (0.0 - 2.0).
  /// </summary>
  public double Temperature { get; init; } = 0.7;

  /// <summary>
  /// Maximum tokens in the response.
  /// </summary>
  public int MaxTokens { get; init; } = 4096;
}
