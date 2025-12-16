using System.Threading.RateLimiting;
using CoverLetter.Application.Common.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

namespace CoverLetter.Api.Extensions;

/// <summary>
/// Extension methods for configuring rate limiting with BYOK (Bring Your Own Key) support.
/// </summary>
public static class RateLimitingExtensions
{
  /// <summary>
  /// Adds rate limiting middleware with intelligent user-based partitioning.
  /// 
  /// How the Sliding Window Rate Limiter Works:
  /// ==========================================
  /// 
  /// Traditional Fixed Window Problem:
  /// - Fixed windows reset at specific times (e.g., every minute at :00)
  /// - Issue: 10 requests at 0:59, then 10 more at 1:00 = 20 requests in 1 second (burst!)
  /// 
  /// Sliding Window Solution:
  /// - Divides the time window into smaller segments
  /// - In our case: 1 minute window ÷ 6 segments = 10-second segments
  /// - Each segment tracks its own request count
  /// - As time progresses, old segments "slide out" of the window
  /// 
  /// Example with our configuration (10 requests/minute, 6 segments):
  /// 
  /// Timeline (each segment = 10 seconds):
  /// [Seg1: 2 req] [Seg2: 3 req] [Seg3: 2 req] [Seg4: 1 req] [Seg5: 2 req] [Seg6: 0 req]
  /// Total in window: 10 requests (at limit)
  /// 
  /// After 10 seconds, oldest segment slides out:
  /// [Seg2: 3 req] [Seg3: 2 req] [Seg4: 1 req] [Seg5: 2 req] [Seg6: 0 req] [New: 0 req]
  /// Total in window: 8 requests (2 permits freed up!)
  /// 
  /// Benefits:
  /// - Smoother rate limiting (no reset spikes)
  /// - Fairer distribution of requests over time
  /// - Better protection against burst attacks
  /// - More predictable for legitimate users
  /// 
  /// BYOK Pattern Integration:
  /// - Users WITH saved API keys: GetNoLimiter() → unlimited requests
  /// - Users WITHOUT saved keys: GetSlidingWindowLimiter() → 10 req/min by IP
  /// 
  /// Queue Behavior:
  /// - When limit reached, 2 additional requests can queue (QueueLimit = 2)
  /// - Processed in FIFO order (OldestFirst)
  /// - Requests beyond queue limit get immediate 429 response
  /// 
  /// Selective Application:
  /// - Applied selectively to expensive LLM endpoints (cover letter generation)
  /// - NOT applied to: settings, health checks, or documentation endpoints
  /// - Endpoints must explicitly call .RequireRateLimiting("ByokPolicy") to enable
  /// </summary>
  public static IServiceCollection AddRateLimitingWithByok(this IServiceCollection services)
  {
    services.AddRateLimiter(options =>
    {
      // Named policy: "ByokPolicy" with BYOK-aware sliding window
      // This policy must be explicitly required by endpoints using .RequireRateLimiting("ByokPolicy")
      options.AddPolicy("ByokPolicy", context =>
          {
            // Resolve IUserContext to check if user has saved their own API key
          var userContext = context.RequestServices.GetRequiredService<IUserContext>();
          var userApiKey = userContext.GetUserApiKey();
          var hasUserApiKey = !string.IsNullOrWhiteSpace(userApiKey);

            // BYOK users get unlimited requests (no rate limiting)
          if (hasUserApiKey)
          {
            return RateLimitPartition.GetNoLimiter("ByokUser");
          }

            // Users without API keys are rate limited by IP address
            // Each IP gets its own sliding window tracker
          var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
          return RateLimitPartition.GetSlidingWindowLimiter(ipAddress, _ => new SlidingWindowRateLimiterOptions
          {
            PermitLimit = 10,  // 10 requests
            Window = TimeSpan.FromMinutes(1),  // per minute
            SegmentsPerWindow = 6,  // 6 segments = smooth sliding
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 2  // Buffer for burst traffic
          });
        });

      // Customize the 429 Too Many Requests response
      options.OnRejected = async (context, cancellationToken) =>
          {
          context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            // Include Retry-After header if available (tells client when to retry)
          if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
          {
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
          }

            // Return helpful JSON response explaining how to bypass limits
          await context.HttpContext.Response.WriteAsJsonAsync(new
          {
            error = "Too many requests",
            message = "Rate limit exceeded. Save your own Groq API key via POST /api/v1/settings/groq-api-key to bypass rate limits.",
            retryAfterSeconds = retryAfter.TotalSeconds
          }, cancellationToken);

            // Log rate limit violations for monitoring
          Log.Warning(
                  "Rate limit exceeded for {IpAddress} - {Path}",
                  context.HttpContext.Connection.RemoteIpAddress,
                  context.HttpContext.Request.Path);
        };
    });

    return services;
  }
}
