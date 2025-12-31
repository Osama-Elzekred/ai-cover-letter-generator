using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Infrastructure.Repositories;

public sealed class CvRepository(
    IMemoryCache cache,
    ILogger<CvRepository> logger) : ICvRepository
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);
    private string GetKey(string cvId) => $"cv:{cvId}";

    public Task<Result<CvDocument>> GetByIdAsync(string cvId, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetKey(cvId);
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
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration,
            Size = 1
        };
        
        cache.Set(GetKey(document.Id), document, cacheOptions);
        logger.LogInformation("CV cached successfully: {CvId}", document.Id);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string cvId, CancellationToken cancellationToken = default)
    {
        cache.Remove(GetKey(cvId));
        return Task.CompletedTask;
    }
}
