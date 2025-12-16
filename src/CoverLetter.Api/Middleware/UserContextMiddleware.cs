namespace CoverLetter.Api.Middleware;

/// <summary>
/// Middleware to extract user identifier from X-User-Id header and make it available to endpoints.
/// Supports anonymous user identification for Phase 1 (no authentication required yet).
/// In Phase 4 (API Gateway), this will be replaced with JWT validation.
/// </summary>
public sealed class UserContextMiddleware(RequestDelegate next, ILogger<UserContextMiddleware> logger)
{
  private const string UserIdHeaderName = "X-User-Id";
  private const string UserIdContextKey = "UserId";

  public async Task InvokeAsync(HttpContext context)
  {
    // Extract user ID from header
    var userId = context.Request.Headers[UserIdHeaderName].FirstOrDefault();

    if (!string.IsNullOrWhiteSpace(userId))
    {
      // Basic validation: should be a valid identifier (not empty, reasonable length)
      if (userId.Length is > 3 and <= 100)
      {
        context.Items[UserIdContextKey] = userId;
        // logger.LogDebug("Request authenticated with User ID: {UserId}", userId);
      }
      else
      {
        logger.LogWarning("Invalid User ID format in header: {UserId}", userId);
      }
    }
    else
    {
      logger.LogDebug("Request without User ID (anonymous)");
    }

    await next(context);
  }
}
