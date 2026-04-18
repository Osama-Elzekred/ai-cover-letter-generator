namespace CoverLetter.Api.Middleware;

using Serilog.Context;

/// <summary>
/// HTTP request logging middleware that logs all requests with appropriate log levels.
/// - 5xx errors → Error level
/// - 4xx errors → Warning level
/// - 2xx/3xx success → Information level
/// - /metrics endpoint → Skipped (Prometheus scrapes frequently)
/// 
/// Enriches logs with structured properties:
/// - RequestPath: the HTTP request path
/// - Elapsed: request duration in milliseconds
/// </summary>
public class HttpRequestLoggingMiddleware
{
  private readonly RequestDelegate _next;
  private readonly ILogger<HttpRequestLoggingMiddleware> _logger;

  public HttpRequestLoggingMiddleware(
      RequestDelegate next,
      ILogger<HttpRequestLoggingMiddleware> logger)
  {
    _next = next;
    _logger = logger;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    var requestPath = context.Request.Path.ToString();

    // Skip logging for /metrics endpoint (Prometheus scrapes frequently)
    if (context.Request.Path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase))
    {
      await _next(context);
      return;
    }

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    await _next(context);
    stopwatch.Stop();

    var statusCode = context.Response.StatusCode;
    var elapsed = stopwatch.ElapsedMilliseconds;

    // Enrich log context with structured properties for Loki parsing
    using (LogContext.PushProperty("RequestPath", requestPath))
    using (LogContext.PushProperty("Elapsed", elapsed))
    using (LogContext.PushProperty("StatusCode", statusCode))
    using (LogContext.PushProperty("RequestMethod", context.Request.Method))
    {
      // Determine log level based on HTTP status code
      var logLevel = statusCode >= 500 ? LogLevel.Error
                   : statusCode >= 400 ? LogLevel.Warning
                   : LogLevel.Information;

      _logger.Log(
          logLevel,
          "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMilliseconds}ms",
          context.Request.Method,
          requestPath,
          statusCode,
          elapsed);
    }
  }
}
