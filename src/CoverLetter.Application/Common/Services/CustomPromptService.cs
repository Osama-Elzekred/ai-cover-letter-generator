using CoverLetter.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.Common.Services;

/// <summary>
/// Implementation using IMemoryCache.
/// Designed to be easily replaced with database-backed implementation in the future.
/// </summary>
public sealed class CustomPromptService : ICustomPromptService
{
  private readonly IMemoryCache _cache;
  private readonly IUserContext _userContext;
  private readonly ICacheKeyBuilder _cacheKeyBuilder;
  private readonly ILogger<CustomPromptService> _logger;

  public CustomPromptService(
      IMemoryCache cache,
      IUserContext userContext,
      ICacheKeyBuilder cacheKeyBuilder,
      ILogger<CustomPromptService> logger)
  {
    _cache = cache;
    _userContext = userContext;
    _cacheKeyBuilder = cacheKeyBuilder;
    _logger = logger;
  }

  public Task<string?> GetUserPromptAsync(PromptType type, CancellationToken cancellationToken = default)
  {
    var userId = _userContext.UserId;
    if (userId == null)
      return Task.FromResult<string?>(null);

    var cacheKey = _cacheKeyBuilder.UserPromptKey(userId, type);

    if (_cache.TryGetValue<string>(cacheKey, out var prompt) && !string.IsNullOrEmpty(prompt))
    {
      _logger.LogDebug("Using saved custom prompt for {PromptType} (user: {UserId})", type, userId);
      return Task.FromResult<string?>(prompt);
    }

    return Task.FromResult<string?>(null);
  }
}
