using CoverLetter.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace CoverLetter.Api.Services;

/// <summary>
/// Implementation of IUserContext that provides access to current user information.
/// Reads from HttpContext.Items (populated by UserContextMiddleware).
/// Scoped per request.
/// </summary>
public sealed class UserContext : IUserContext
{
  private const string UserIdContextKey = "UserId";
  private readonly IHttpContextAccessor _httpContextAccessor;
  private readonly IMemoryCache _cache;
  private readonly ICacheKeyBuilder _cacheKeyBuilder;

  public UserContext(
      IHttpContextAccessor httpContextAccessor,
      IMemoryCache cache,
      ICacheKeyBuilder cacheKeyBuilder)
  {
    _httpContextAccessor = httpContextAccessor;
    _cache = cache;
    _cacheKeyBuilder = cacheKeyBuilder;
  }

  public string? UserId
  {
    get
    {
      var context = _httpContextAccessor.HttpContext;
      if (context is null) return null;

      return context.Items.TryGetValue(UserIdContextKey, out var userId)
          ? userId as string
          : null;
    }
  }

  public string? GetUserApiKey()
  {
    if (string.IsNullOrWhiteSpace(UserId))
    {
      return null;
    }

    var cacheKey = _cacheKeyBuilder.UserApiKey(UserId);
    return _cache.TryGetValue<string>(cacheKey, out var apiKey) ? apiKey : null;
  }
}
