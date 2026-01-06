using CoverLetter.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.Common.Extensions;

/// <summary>
/// Extension methods for enriching log scopes with business context.
/// Follows production observability patterns used by Netflix, Uber, etc.
/// </summary>
public static class LoggingExtensions
{
  /// <summary>
  /// Creates a logging scope enriched with user context and operation details.
  /// All logs within this scope will automatically include the provided metadata.
  /// </summary>
  /// <param name="logger">The logger instance</param>
  /// <param name="userContext">Current user context (provides UserId)</param>
  /// <param name="operation">Name of the business operation being performed</param>
  /// <param name="additionalProperties">Optional additional properties to include in scope</param>
  /// <returns>Disposable scope - use with 'using' statement</returns>
  /// <example>
  /// <code>
  /// using var scope = logger.BeginHandlerScope(userContext, "CustomizeCv", new()
  /// {
  ///     ["CvId"] = request.CvId,
  ///     ["HasCustomPrompt"] = !string.IsNullOrEmpty(customPrompt)
  /// });
  /// </code>
  /// </example>
  public static IDisposable? BeginHandlerScope(
      this ILogger logger,
      IUserContext userContext,
      string operation,
      Dictionary<string, object>? additionalProperties = null)
  {
    var properties = new Dictionary<string, object>
    {
      ["UserId"] = userContext.UserId ?? "anonymous",
      ["Operation"] = operation
    };

    if (additionalProperties != null)
    {
      foreach (var kvp in additionalProperties)
      {
        properties[kvp.Key] = kvp.Value;
      }
    }

    return logger.BeginScope(properties);
  }
}
