using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs request execution with duration.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
  private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

  public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
  {
    _logger = logger;
  }

  public async Task<TResponse> Handle(
      TRequest request,
      RequestHandlerDelegate<TResponse> next,
      CancellationToken cancellationToken)
  {
    var requestName = typeof(TRequest).Name;
    var stopwatch = Stopwatch.StartNew();

    try
    {
      var response = await next();
      stopwatch.Stop();

      _logger.LogInformation(
          "{RequestName} completed in {ElapsedMs}ms",
          requestName,
          stopwatch.ElapsedMilliseconds);

      return response;
    }
    catch (Exception ex)
    {
      stopwatch.Stop();

      _logger.LogError(
          ex,
          "{RequestName} failed after {ElapsedMs}ms",
          requestName,
          stopwatch.ElapsedMilliseconds);

      throw;
    }
  }
}
