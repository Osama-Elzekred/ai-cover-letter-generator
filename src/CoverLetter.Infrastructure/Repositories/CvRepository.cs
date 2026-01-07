using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Infrastructure.Repositories;

public sealed class CvRepository(
    IMemoryCache cache,
    ICacheKeyBuilder cacheKeyBuilder,
    ILogger<CvRepository> logger) : ICvRepository
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    public Task<Result<CvDocument>> GetByIdAsync(string cvId, CancellationToken cancellationToken = default)
    {
        var cacheKey = cacheKeyBuilder.CvKey(cvId);
        if (cache.TryGetValue<CvDocument>(cacheKey, out var document) && document != null)
        {
            return Task.FromResult(Result<CvDocument>.Success(document));
        }

        logger.LogWarning("CV not found in cache: {CvId}", cvId);
        return Task.FromResult(Result<CvDocument>.Failure(
            $"CV with ID '{cvId}' not found. It may have expired or is invalid.",
            ResultType.NotFound));
    }

    public Task SaveAsync(CvDocument document, CancellationToken cancellationToken = default)
    {
        var cacheKey = cacheKeyBuilder.CvKey(document.Id);
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
            // Size not needed when SizeLimit is disabled
        };

        cache.Set(cacheKey, document, cacheOptions);
        logger.LogInformation("CV cached successfully: {CvId}", document.Id);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string cvId, CancellationToken cancellationToken = default)
    {
        var cacheKey = cacheKeyBuilder.CvKey(cvId);
        cache.Remove(cacheKey);
        return Task.CompletedTask;
    }
}
