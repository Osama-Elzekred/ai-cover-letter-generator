using Prometheus;

namespace CoverLetter.Infrastructure.Messaging;

/// <summary>
/// Prometheus metrics for the compile pipeline.
/// Exposed via the /metrics endpoint alongside the standard ASP.NET Core metrics.
/// </summary>
public static class CompilePipelineMetrics
{
    /// <summary>
    /// Histogram of compile durations in seconds, labelled by outcome (success/failure).
    /// </summary>
    public static readonly Histogram CompileDurationSeconds = Metrics.CreateHistogram(
        "compile_duration_seconds",
        "LaTeX compile duration in seconds.",
        new HistogramConfiguration
        {
            LabelNames = new[] { "outcome" },
            Buckets = Histogram.ExponentialBuckets(0.5, 2, 8) // 0.5s .. 64s
        });

    /// <summary>
    /// Counter of compile completions, labelled by outcome.
    /// </summary>
    public static readonly Counter CompileJobsTotal = Metrics.CreateCounter(
        "compile_jobs_total",
        "Total compile jobs processed by outcome.",
        new CounterConfiguration
        {
            LabelNames = new[] { "outcome" }
        });

    /// <summary>
    /// Gauge of undispatched outbox messages (backlog).
    /// Updated by the outbox dispatcher each poll cycle.
    /// </summary>
    public static readonly Gauge OutboxBacklog = Metrics.CreateGauge(
        "compile_outbox_backlog",
        "Number of undispatched outbox messages waiting to be published.");

    /// <summary>
    /// Counter of outbox dispatch attempts, labelled by outcome (success/failure).
    /// </summary>
    public static readonly Counter OutboxDispatchTotal = Metrics.CreateCounter(
        "compile_outbox_dispatch_total",
        "Total outbox dispatch attempts by outcome.",
        new CounterConfiguration
        {
            LabelNames = new[] { "outcome" }
        });
}
