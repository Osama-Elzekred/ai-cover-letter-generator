using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoverLetter.Api.HealthChecks;

/// <summary>
/// Health check that verifies the memory cache is accessible and responsive.
/// </summary>
public sealed class MemoryCacheHealthCheck : IHealthCheck
{
  private readonly IMemoryCache _cache;

  public MemoryCacheHealthCheck(IMemoryCache cache)
  {
    _cache = cache;
  }

  public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
  {
    try
    {
      // Test cache write/read
      var testKey = "health_check_test";
      var testValue = DateTime.UtcNow;

      _cache.Set(testKey, testValue);
      var success = _cache.TryGetValue(testKey, out var cached) &&
                    cached is DateTime cachedTime &&
                    cachedTime == testValue;

      _cache.Remove(testKey);

      if (success)
      {
        return Task.FromResult(HealthCheckResult.Healthy("Memory cache is operational"));
      }

      return Task.FromResult(HealthCheckResult.Unhealthy("Memory cache test read/write failed"));
    }
    catch (Exception ex)
    {
      return Task.FromResult(HealthCheckResult.Unhealthy("Memory cache check failed", ex));
    }
  }
}
