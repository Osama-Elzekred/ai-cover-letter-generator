namespace CoverLetter.Application.Common.Interfaces;

/// <summary>
/// Provides access to the current user's context within the application.
/// Abstracts user identification and preferences from infrastructure concerns.
/// Scoped per request - registered as a scoped service.
/// </summary>
/// <remarks>
/// This follows ASP.NET Core patterns like IHttpContextAccessor.
/// User information is populated by middleware and available throughout the request pipeline.
/// </remarks>
public interface IUserContext
{
  /// <summary>
  /// Gets the current user's identifier.
  /// Extracted from X-User-Id header by UserContextMiddleware.
  /// </summary>
  /// <returns>User ID if authenticated, null for anonymous requests</returns>
  string? UserId { get; }

  /// <summary>
  /// Retrieves the user's saved Groq API key if one exists.
  /// Used for BYOK (Bring Your Own Key) pattern - users with saved keys bypass rate limits.
  /// </summary>
  /// <returns>User's API key if saved, null if using default</returns>
  string? GetUserApiKey();

  /// <summary>
  /// Checks if the current request has an authenticated user.
  /// </summary>
  bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserId);
}
