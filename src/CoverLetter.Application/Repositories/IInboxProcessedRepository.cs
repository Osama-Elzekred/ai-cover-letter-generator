namespace CoverLetter.Application.Repositories;

/// <summary>
/// Repository for the worker-side inbox deduplication table.
/// A message id present here means the job has already been processed and a
/// redelivery must be skipped, achieving safe at-least-once delivery.
/// </summary>
public interface IInboxProcessedRepository
{
  /// <summary>
  /// Returns true if the message id has already been processed.
  /// </summary>
  Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken = default);

  /// <summary>
  /// Records a processed message. Saved in the same transaction as the compile job
  /// status update so deduplication and job result are committed atomically.
  /// </summary>
  Task AddAsync(InboxProcessedDto entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO to avoid leaking EF entities into the Application layer.
/// </summary>
public record InboxProcessedDto
{
  public Guid MessageId { get; init; }
  public DateTime ProcessedAt { get; init; }
  public Guid? JobId { get; init; }
}
