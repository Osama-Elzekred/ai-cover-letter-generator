namespace CoverLetter.Application.Common.Interfaces;

/// <summary>
/// Service for loading user's custom prompts.
/// Abstraction designed to support future migration from IMemoryCache to database.
/// </summary>
public interface ICustomPromptService
{
  /// <summary>
  /// Retrieves the current user's custom prompt for the specified type.
  /// Returns null if no custom prompt is saved.
  /// </summary>
  Task<string?> GetUserPromptAsync(PromptType type, CancellationToken cancellationToken = default);
}
