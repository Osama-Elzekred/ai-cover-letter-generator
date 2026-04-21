using Prometheus;

namespace CoverLetter.Api.Endpoints;

/// <summary>
/// Receives lightweight observability events from the browser extension.
/// Events are logged to Serilog (flowing to Loki) and counted in Prometheus metrics.
/// </summary>
public static class ExtensionObservabilityEndpoints
{
    private static readonly Counter ExtensionEventsTotal = Metrics.CreateCounter(
        "extension_events_total",
        "Total extension events received by the API.",
        new CounterConfiguration
        {
            LabelNames = new[] { "event_type", "source", "status", "action" }
        });

    private static readonly Histogram ExtensionEventDurationMs = Metrics.CreateHistogram(
        "extension_event_duration_ms",
        "Duration for extension events in milliseconds when provided.",
        new HistogramConfiguration
        {
            LabelNames = new[] { "event_type", "source", "action" },
            Buckets = Histogram.ExponentialBuckets(10, 2, 10)
        });

    public static IEndpointRouteBuilder MapExtensionObservabilityEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/observability")
            .WithTags("Observability");

        group.MapPost("/extension/events", IngestExtensionEvent)
            .WithSummary("Ingest extension event")
            .WithDescription("Receives extension logs/activities for Loki logging and Prometheus metrics.")
            .Produces<ExtensionEventResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        return routes;
    }

    private static IResult IngestExtensionEvent(ExtensionEventRequest request, ILoggerFactory loggerFactory)
    {
        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            return Results.BadRequest(new { error = "eventType is required." });
        }

        var logger = loggerFactory.CreateLogger("ExtensionObservability");

        var eventType = SanitizeLabel(request.EventType, "unknown");
        var source = SanitizeLabel(request.Source, "extension");
        var status = request.Success ? "success" : "failure";
        var action = ExtractAction(request.Metadata, eventType);

        ExtensionEventsTotal.WithLabels(eventType, source, status, action).Inc();

        if (request.DurationMs is > 0)
        {
            ExtensionEventDurationMs.WithLabels(eventType, source, action).Observe(request.DurationMs.Value);
        }

        var logLevel = ResolveLogLevel(request);
        var detail = BuildDetail(request, eventType, status);
        var durationMs = request.DurationMs ?? 0;
        var statusCodeText = TryGetStatusCode(request.Metadata, out var statusCode)
            ? statusCode.ToString()
            : "n/a";

        logger.Log(
            logLevel,
            "{Detail:l} {StatusCode} {DurationMs}ms",
            detail,
            statusCodeText,
            durationMs);

        return Results.Ok(new ExtensionEventResponse(true));
    }

    private static string SanitizeLabel(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_')
            .ToArray();

        return new string(chars).Length > 60
            ? new string(chars, 0, 60)
            : new string(chars);
    }

    private static string ExtractAction(IReadOnlyDictionary<string, string>? metadata, string fallback)
    {
        if (metadata is not null)
        {
            foreach (var pair in metadata)
            {
                if (pair.Key.Equals("operation", StringComparison.OrdinalIgnoreCase) ||
                    pair.Key.Equals("action", StringComparison.OrdinalIgnoreCase))
                {
                    return SanitizeLabel(pair.Value, fallback);
                }
            }
        }

        return SanitizeLabel(fallback, "unknown");
    }

    private static string BuildDetail(ExtensionEventRequest request, string eventType, string status)
    {
        var metadata = request.Metadata;
        if (metadata is not null &&
            metadata.TryGetValue("method", out var method) &&
            metadata.TryGetValue("endpoint", out var endpoint) &&
            !string.IsNullOrWhiteSpace(method) &&
            !string.IsNullOrWhiteSpace(endpoint))
        {
            var outcome = request.Success ? "succeeded" : "failed";
            return $"{method.ToUpperInvariant()} {endpoint} {outcome}";
        }

        if (!string.IsNullOrWhiteSpace(request.Message))
        {
            return request.Message;
        }

        return $"{eventType} {status}";
    }

    private static LogLevel ResolveLogLevel(ExtensionEventRequest request)
    {
        if (TryGetStatusCode(request.Metadata, out var statusCode))
        {
            if (statusCode >= 500)
            {
                return LogLevel.Error;
            }

            if (statusCode >= 400)
            {
                return LogLevel.Warning;
            }

            return LogLevel.Information;
        }

        var level = request.Level?.Trim().ToLowerInvariant();
        if (level == "error")
        {
            return LogLevel.Error;
        }

        if (level == "warning" || level == "warn")
        {
            return LogLevel.Warning;
        }

        if (level == "info" || level == "information")
        {
            return LogLevel.Information;
        }

        return request.Success ? LogLevel.Information : LogLevel.Warning;
    }

    private static bool TryGetStatusCode(IReadOnlyDictionary<string, string>? metadata, out int statusCode)
    {
        statusCode = 0;

        if (metadata is null || !metadata.TryGetValue("status", out var rawStatus))
        {
            return false;
        }

        return int.TryParse(rawStatus, out statusCode);
    }
}

public sealed record ExtensionEventRequest(
    string EventType,
    string? Source,
    string? Message,
    string? Level,
    bool Success = true,
    long? DurationMs = null,
    Dictionary<string, string>? Metadata = null);

public sealed record ExtensionEventResponse(bool Accepted);
