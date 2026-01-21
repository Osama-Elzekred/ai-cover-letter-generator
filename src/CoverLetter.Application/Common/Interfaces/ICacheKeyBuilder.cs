using CoverLetter.Domain.Enums;

namespace CoverLetter.Application.Common.Interfaces;

/// <summary>
/// Builds consistent cache keys for application data.
/// Centralizes cache key generation to prevent typos and make future migrations easier.
/// </summary>
public interface ICacheKeyBuilder
{
  /// <summary>
  /// Generates cache key for user's custom prompt.
  /// </summary>
  string UserPromptKey(string userId, PromptType type);

  /// <summary>
  /// Generates cache key for user's API key (BYOK pattern).
  /// </summary>
  string UserApiKey(string userId);

  /// <summary>
  /// Generates cache key for uploaded CV document.
  /// </summary>
  string CvKey(string cvId);
}
