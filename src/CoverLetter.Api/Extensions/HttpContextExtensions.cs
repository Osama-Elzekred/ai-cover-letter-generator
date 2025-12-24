namespace CoverLetter.Api.Extensions;

/// <summary>
/// Extension methods for accessing user context from HttpContext.
/// </summary>
public static class HttpContextExtensions
{
  /// <summary>
  /// Gets the idempotency key from the request headers (if present).
  /// </summary>
  /// <param name="context">The HTTP context</param>
  /// <returns>Idempotency key if present, otherwise null</returns>
  public static string? GetIdempotencyKey(this HttpContext context)
  {
    return context.Request.Headers["X-Idempotency-Key"].FirstOrDefault();
  }


  private const string UserIdContextKey = "UserId";

  /// <summary>
  /// Gets the current user ID from the request context.
  /// User ID is extracted from X-User-Id header by UserContextMiddleware.
  /// </summary>
  /// <param name="context">The HTTP context</param>
  /// <returns>User ID if present, null if anonymous request</returns>
  public static string? GetUserId(this HttpContext context)
  {
    return context.Items.TryGetValue(UserIdContextKey, out var userId)
        ? userId as string
        : null;
  }

  /// <summary>
  /// Checks if the current request has an authenticated user.
  /// </summary>
  /// <param name="context">The HTTP context</param>
  /// <returns>True if user ID is present, false for anonymous requests</returns>
  public static bool HasUserId(this HttpContext context)
  {
    return !string.IsNullOrWhiteSpace(context.GetUserId());
  }

  /// <summary>
  /// Gets the user ID or throws an exception if not present.
  /// Use this for endpoints that require authentication.
  /// </summary>
  /// <param name="context">The HTTP context</param>
  /// <returns>User ID</returns>
  /// <exception cref="UnauthorizedAccessException">When user ID is not present</exception>
  public static string GetRequiredUserId(this HttpContext context)
  {
    var userId = context.GetUserId();
    if (string.IsNullOrWhiteSpace(userId))
    {
      throw new UnauthorizedAccessException("User ID is required. Include X-User-Id header in your request.");
    }
    return userId;
  }
}
