using CoverLetter.Application.Common.Extensions;
using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Enums;
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
  private readonly IUserPromptRepository _repository;
  private readonly IUnitOfWork _unitOfWork;
  private readonly ILogger<CustomPromptService> _logger;

  private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

  public CustomPromptService(
      IMemoryCache cache,
      IUserContext userContext,
      ICacheKeyBuilder cacheKeyBuilder,
      IUserPromptRepository repository,
      IUnitOfWork unitOfWork,
      ILogger<CustomPromptService> logger)
  {
    _cache = cache;
    _userContext = userContext;
    _cacheKeyBuilder = cacheKeyBuilder;
    _repository = repository;
    _unitOfWork = unitOfWork;
    _logger = logger;
  }

  public Task<string?> GetUserPromptAsync(PromptType type, CancellationToken cancellationToken = default)
  {
    var userId = _userContext.UserId;
    if (userId == null)
      return Task.FromResult<string?>(null);

    using var scope = _logger.BeginHandlerScope(_userContext, "GetUserPrompt", new()
    {
      ["PromptType"] = type.ToString()
    });

    var cacheKey = _cacheKeyBuilder.UserPromptKey(userId, type);

    if (_cache.TryGetValue<string>(cacheKey, out var prompt) && !string.IsNullOrEmpty(prompt))
    {
      _logger.LogDebug("Custom prompt found for {PromptType} (cache hit)", type);
      return Task.FromResult<string?>(prompt);
    }

    return GetFromStoreAsync(userId, type, cacheKey, cancellationToken);
  }

  public async Task SaveUserPromptAsync(PromptType type, string prompt, CancellationToken cancellationToken = default)
  {
    var userId = _userContext.UserId;
    if (userId == null)
      throw new InvalidOperationException("User ID is required to save custom prompts.");

    var now = DateTime.UtcNow;

    var dto = new UserPromptDto
    {
      Id = Guid.NewGuid(),
      UserId = Guid.Parse(userId),
      PromptType = type,
      Content = prompt,
      CreatedAt = now,
      UpdatedAt = now
    };

    await _repository.UpsertAsync(dto, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    var cacheKey = _cacheKeyBuilder.UserPromptKey(userId, type);
    _cache.Set(cacheKey, prompt, new MemoryCacheEntryOptions
    {
      AbsoluteExpirationRelativeToNow = CacheDuration
    });

    // _logger.LogInformation("Custom prompt saved for {PromptType}", type);
  }

  public async Task DeleteUserPromptAsync(PromptType type, CancellationToken cancellationToken = default)
  {
    var userId = _userContext.UserId;
    if (userId == null)
      throw new InvalidOperationException("User ID is required to delete custom prompts.");

    await _unitOfWork.SaveChangesAsync(cancellationToken);
    await _repository.DeleteAsync(Guid.Parse(userId), type, cancellationToken);

    var cacheKey = _cacheKeyBuilder.UserPromptKey(userId, type);
    _cache.Remove(cacheKey);

    _logger.LogInformation("Custom prompt deleted for {PromptType}", type);
  }

  private async Task<string?> GetFromStoreAsync(string userId, PromptType type, string cacheKey, CancellationToken cancellationToken)
  {
    var prompt = await _repository.GetAsync(Guid.Parse(userId), type, cancellationToken);

    if (prompt?.Content is { Length: > 0 } content)
    {
      _cache.Set(cacheKey, content, new MemoryCacheEntryOptions
      {
        AbsoluteExpirationRelativeToNow = CacheDuration
      });

      _logger.LogDebug("Custom prompt found for {PromptType} (db hit)", type);
      return content;
    }

    _logger.LogDebug("No custom prompt found for {PromptType} (miss)", type);
    return null;
  }
}
