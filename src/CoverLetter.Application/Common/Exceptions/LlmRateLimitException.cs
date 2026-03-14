namespace CoverLetter.Application.Common.Exceptions;

/// <summary>
/// Raised when the upstream LLM provider rejects a request due to rate limiting.
/// </summary>
public sealed class LlmRateLimitException(string message) : Exception(message);
