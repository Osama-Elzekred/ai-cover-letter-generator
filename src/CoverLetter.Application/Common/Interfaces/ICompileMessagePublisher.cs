namespace CoverLetter.Application.Common.Interfaces;

/// <summary>
/// Message contract published to the broker whenever a compile job is enqueued.
/// The worker consumer deserialises this payload to perform the compilation.
/// <see cref="MessageId"/> is used for inbox deduplication on the worker side.
/// </summary>
public sealed record CompileJobMessage
{
  public required Guid JobId { get; init; }
  public required string? UserId { get; init; }
  public required string? IdempotencyKey { get; init; }
  public required string Latex { get; init; }
  public required CompileOptions Options { get; init; }
}

/// <summary>
/// Compile options forwarded to the worker.
/// </summary>
public sealed record CompileOptions
{
  /// <summary>
  /// Output target. Currently only "pdf" is supported.
  /// </summary>
  public string Target { get; init; } = "pdf";

  /// <summary>
  /// Hard timeout for the latexmk invocation, in seconds.
  /// </summary>
  public int TimeoutSeconds { get; init; } = 30;
}

/// <summary>
/// Publishes compile job messages to the message broker.
/// Implemented by the RabbitMQ adapter in Infrastructure and consumed by the
/// OutboxDispatcher background service.
/// </summary>
public interface ICompileMessagePublisher
{
  /// <summary>
  /// Publishes a message with the given message id and payload.
  /// The message id is set as the broker message id for inbox deduplication.
  /// </summary>
  Task PublishAsync(Guid messageId, string topic, string payload, CancellationToken cancellationToken = default);
}
