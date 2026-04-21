namespace CoverLetter.Api.Configuration;

/// <summary>
/// Configuration for logging, metrics, and tracing instrumentation.
/// Loaded from appsettings.json[Observability] section.
/// </summary>
public class ObservabilitySettings
{
  /// <summary>
  /// Threshold in milliseconds to flag requests as "slow" for metrics collection.
  /// Default: 100ms
  /// </summary>
  public int SlowRequestThresholdMs { get; set; } = 100;

  /// <summary>
  /// System timezone name for log enrichment (e.g., "Egypt Standard Time", "UTC", "America/New_York").
  /// Must be a valid Windows or IANA timezone identifier.
  /// Default: "UTC"
  /// </summary>
  public string LogTimeZone { get; set; } = "UTC";

  /// <summary>
  /// UTC offset string appended to timestamps (e.g., "+02:00", "+00:00").
  /// Used in formatted log output for clarity.
  /// Default: "+00:00"
  /// </summary>
  public string LogTimeZoneOffset { get; set; } = "+00:00";

  /// <summary>
  /// Metrics retention period in LogQL format (e.g., "1m", "5m", "1h").
  /// Used in Grafana dashboard queries.
  /// Default: "5m"
  /// </summary>
  public string MetricsRetention { get; set; } = "5m";
}
