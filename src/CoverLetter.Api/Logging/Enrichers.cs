using Serilog.Core;
using Serilog.Events;
using CoverLetter.Api.Configuration;

namespace CoverLetter.Api.Logging;

/// <summary>
/// Enriches Serilog log events with timezone-aware timestamp in the configured timezone.
/// Adds a "Timestamp" property with format: yyyy-MM-ddTHH:mm:ss.f[±HH:MM]
/// </summary>
public class TimestampEnricher : ILogEventEnricher
{
  private readonly ObservabilitySettings _settings;

  public TimestampEnricher(ObservabilitySettings settings)
  {
    _settings = settings;
  }

  public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
  {
    // Convert UTC to configured timezone with error handling
    TimeZoneInfo timeZone;
    try
    {
      timeZone = TimeZoneInfo.FindSystemTimeZoneById(_settings.LogTimeZone);
    }
    catch
    {
      // Fallback to UTC if timezone name is invalid
      timeZone = TimeZoneInfo.Utc;
    }

    var localTime = TimeZoneInfo.ConvertTime(logEvent.Timestamp.UtcDateTime, timeZone);
    var timestamp = localTime.ToString("yyyy-MM-ddTHH:mm:ss.f") + _settings.LogTimeZoneOffset;
    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Timestamp", timestamp));
  }
}

/// <summary>
/// Enriches Serilog log events with human-readable formatted log string using the configured timezone.
/// Adds a "FormattedLog" property with format: [HH:mm:ss LVL] Message
/// Useful for display in Grafana logs panels via LogQL line_format directive.
/// </summary>
public class FormattedLogEnricher : ILogEventEnricher
{
  private readonly ObservabilitySettings _settings;

  public FormattedLogEnricher(ObservabilitySettings settings)
  {
    _settings = settings;
  }

  public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
  {
    // Format: [HH:MM:SS LVL] Message — using configured timezone for consistency
    TimeZoneInfo timeZone;
    try
    {
      timeZone = TimeZoneInfo.FindSystemTimeZoneById(_settings.LogTimeZone);
    }
    catch
    {
      // Fallback to UTC if timezone name is invalid
      timeZone = TimeZoneInfo.Utc;
    }

    var localTime = TimeZoneInfo.ConvertTime(logEvent.Timestamp.UtcDateTime, timeZone);
    var time = localTime.ToString("HH:mm:ss");
    var level = logEvent.Level.ToString().Substring(0, 3).ToUpper();
    var message = logEvent.MessageTemplate.Render(logEvent.Properties);

    var formattedLog = $"[{time} {level}] {message}";
    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("FormattedLog", formattedLog));
  }
}
