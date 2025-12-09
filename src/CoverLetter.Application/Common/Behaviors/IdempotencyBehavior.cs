using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that handles idempotency by caching responses.
/// Prevents duplicate processing of identical requests within a time window.
/// </summary>
public sealed class IdempotencyBehavior<TRequest, TResponse>(
    IMemoryCache cache,
    ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IIdempotentRequest
{
  private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromHours(24);

  public async Task<TResponse> Handle(
      TRequest request,
      RequestHandlerDelegate<TResponse> next,
      CancellationToken cancellationToken)
  {
    // If no idempotency key provided, skip caching
    if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
    {
      return await next();
    }

    var cacheKey = $"idempotency:{typeof(TRequest).Name}:{request.IdempotencyKey}";

    // Try to get cached response
    if (cache.TryGetValue<TResponse>(cacheKey, out var cachedResponse))
    {
      logger.LogInformation(
          "Returning cached response for idempotency key {IdempotencyKey} (Request: {RequestType})",
          request.IdempotencyKey,
          typeof(TRequest).Name);

      return cachedResponse!;
    }

    // Execute the handler
    var response = await next();

    // Cache the response
    var cacheOptions = new MemoryCacheEntryOptions
    {
      AbsoluteExpirationRelativeToNow = DefaultCacheDuration,
      Size = 1  // For cache size limiting
    };

    cache.Set(cacheKey, response, cacheOptions);

    logger.LogDebug(
        "Cached response for idempotency key {IdempotencyKey} (expires in {Duration})",
        request.IdempotencyKey,
        DefaultCacheDuration);

    return response;
  }
}

/// <summary>
/// Marker interface for requests that support idempotency.
/// Implement this on commands/queries that should be idempotent.
/// </summary>
public interface IIdempotentRequest
{
  /// <summary>
  /// Unique key for this request. Same key = same response returned from cache.
  /// </summary>
  string? IdempotencyKey { get; }
}
