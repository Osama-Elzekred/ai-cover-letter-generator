using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Enums;
using Microsoft.Extensions.Caching.Memory;

namespace CoverLetter.Api.Middleware;

/// <summary>
/// Middleware to extract user identifier from X-User-Id header and make it available to endpoints.
/// Supports anonymous user identification for Phase 1 (no authentication required yet).
/// In Phase 4 (API Gateway), this will be replaced with JWT validation.
/// </summary>
public sealed class UserContextMiddleware(
  RequestDelegate next,
  ILogger<UserContextMiddleware> logger,
  IMemoryCache cache)
{
  private const string UserIdHeaderName = "X-User-Id";
  private const string UserIdContextKey = "UserId";
  private static readonly TimeSpan ApiKeyCacheDuration = TimeSpan.FromDays(30);

  // Scoped services (ICacheKeyBuilder, IUserApiKeyRepository) are injected via InvokeAsync
  // rather than the constructor, because middleware instances are effectively singletons —
  // constructor-injected scoped services would be captured from the root container.
  public async Task InvokeAsync(
    HttpContext context,
    ICacheKeyBuilder cacheKeyBuilder,
    IUserApiKeyRepository userApiKeyRepository)
  {
    var userId = context.Request.Headers[UserIdHeaderName].FirstOrDefault();

    if (!string.IsNullOrWhiteSpace(userId))
    {
      if (userId.Length is > 3 and <= 100)
      {
        context.Items[UserIdContextKey] = userId;
        await PreloadApiKeysAsync(userId, cacheKeyBuilder, userApiKeyRepository, context.RequestAborted);
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

  // Preloads all known providers so any BYOK lookup during the request is a pure cache hit.
  private async Task PreloadApiKeysAsync(
    string userId,
    ICacheKeyBuilder cacheKeyBuilder,
    IUserApiKeyRepository userApiKeyRepository,
    CancellationToken cancellationToken)
  {
    foreach (var provider in Enum.GetValues<LlmProvider>())
    {
      var cacheKey = cacheKeyBuilder.UserApiKey(userId, provider);
      if (cache.TryGetValue<string>(cacheKey, out _))
        continue;

      var persisted = await userApiKeyRepository.GetAsync(userId, provider, cancellationToken);
      if (!string.IsNullOrWhiteSpace(persisted?.ApiKey))
      {
        cache.Set(cacheKey, persisted.ApiKey, ApiKeyCacheDuration);
        logger.LogDebug("Preloaded {Provider} API key into cache for user {UserId}", provider, userId);
      }
    }
  }
}
