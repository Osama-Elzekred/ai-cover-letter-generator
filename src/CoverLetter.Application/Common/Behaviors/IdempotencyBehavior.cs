using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that handles idempotency by caching responses.
/// Prevents duplicate processing of identical requests within a time window.
/// Uses GetOrCreateAsync for thread-safe cache access.
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

    // Thread-safe: GetOrCreateAsync uses internal locking per cache key
    var response = await cache.GetOrCreateAsync(cacheKey, async entry =>
    {
      entry.AbsoluteExpirationRelativeToNow = DefaultCacheDuration;
      entry.Size = 1;

      logger.LogDebug(
          "Cache miss for idempotency key {IdempotencyKey}, executing handler",
          request.IdempotencyKey);

      return await next();
    });

    // Log if we returned cached response (response will be null only if factory returned null)
    if (response != null)
    {
      logger.LogInformation(
          "Processed request with idempotency key {IdempotencyKey} (Request: {RequestType})",
          request.IdempotencyKey,
          typeof(TRequest).Name);
    }

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
