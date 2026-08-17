namespace CoverLetter.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for the LaTeX compile worker pipeline
/// (outbox dispatcher + consumer + storage).
/// Bound from the "CompileWorker" configuration section.
/// </summary>
public sealed class CompileWorkerSettings
{
  public const string SectionName = "CompileWorker";

  /// <summary>
  /// Maximum number of concurrent compile jobs the consumer will process.
  /// Maps to the RabbitMQ consumer prefetch count.
  /// </summary>
  public int MaxConcurrency { get; init; } = 2;

  /// <summary>
  /// Hard timeout for a single latexmk invocation, in seconds.
  /// </summary>
  public int CompileTimeoutSeconds { get; init; } = 30;

  /// <summary>
  /// Directory where compiled PDFs are persisted for later download.
  /// </summary>
  public string StoragePath { get; init; } = "/data/pdfs";

  /// <summary>
  /// Interval at which the outbox dispatcher polls for unsent messages, in milliseconds.
  /// </summary>
  public int OutboxPollIntervalMs { get; init; } = 1000;

  /// <summary>
  /// Number of outbox rows fetched per dispatcher poll cycle.
  /// </summary>
  public int OutboxBatchSize { get; init; } = 50;

  /// <summary>
  /// Maximum delivery attempts before an outbox message is abandoned.
  /// </summary>
  public int MaxOutboxAttempts { get; init; } = 5;

  /// <summary>
  /// Base delay for exponential backoff between failed outbox dispatch attempts, in seconds.
  /// </summary>
  public int OutboxBackoffBaseSeconds { get; init; } = 2;

  /// <summary>
  /// Number of retry attempts for MassTransit message redelivery.
  /// </summary>
  public int MassTransitRetryAttempts { get; init; } = 5;

  /// <summary>
  /// Minimum delay for MassTransit retry policy, in seconds.
  /// </summary>
  public int MassTransitRetryMinSeconds { get; init; } = 1;

  /// <summary>
  /// Maximum delay for MassTransit retry policy, in seconds.
  /// </summary>
  public int MassTransitRetryMaxSeconds { get; init; } = 30;

  /// <summary>
  /// Delay delta for exponential backoff in MassTransit retry policy, in seconds.
  /// </summary>
  public int MassTransitRetryIntervalSeconds { get; init; } = 2;
}
