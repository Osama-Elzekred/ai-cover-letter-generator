using System.Text.RegularExpressions;
using CoverLetter.Application.Common.Interfaces;

namespace CoverLetter.Application.Common.Services;

/// <summary>
/// Centralized cache key generation for consistent naming across the application.
/// Makes it easy to change key format or migrate to a different storage system.
/// </summary>
public sealed partial class CacheKeyBuilder : ICacheKeyBuilder
{
  public string UserPromptKey(string userId, PromptType type)
  {
    var kebabCaseType = ToKebabCase(type.ToString());
    return $"custom_prompt_{kebabCaseType}_{userId}";
  }

  public string UserApiKey(string userId)
  {
    return $"user:{userId}:groq-api-key";
  }

  /// <summary>
  /// Converts PascalCase to kebab-case (e.g., CvCustomization -> cv-customization)
  /// </summary>
  private static string ToKebabCase(string input)
  {
    if (string.IsNullOrEmpty(input))
      return input;

    return KebabCaseRegex()
        .Replace(input, "-$1")
        .Trim('-')
        .ToLowerInvariant();
  }

  [GeneratedRegex("(?<!^)([A-Z])")]
  private static partial Regex KebabCaseRegex();
}
