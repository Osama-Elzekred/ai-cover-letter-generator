namespace CoverLetter.Application.Repositories;

/// <summary>
/// Repository for the transactional outbox.
/// Messages are written in the same transaction as the compile job and
/// dispatched to the broker by the OutboxDispatcher background service.
/// </summary>
public interface IOutboxMessageRepository
{
  /// <summary>
  /// Adds a new outbox message. SaveChangesAsync called via IUnitOfWork so the
  /// message and its compile job are committed in a single transaction.
  /// </summary>
  Task AddAsync(OutboxMessageDto message, CancellationToken cancellationToken = default);

  /// <summary>
  /// Fetches a batch of undispatched messages whose next-attempt time has elapsed.
  /// Ordered by id for deterministic dispatch.
  /// </summary>
  Task<IReadOnlyList<OutboxMessageDto>> GetUndispatchedBatchAsync(int batchSize, CancellationToken cancellationToken = default);

  /// <summary>
  /// Marks a message as dispatched (delivered to the broker).
  /// </summary>
  Task MarkDispatchedAsync(long id, CancellationToken cancellationToken = default);

  /// <summary>
  /// Records a failed dispatch attempt: increments attempts and sets the next attempt time
  /// using exponential backoff. Returns true if the message is still within the retry budget,
  /// false if it has exceeded MaxAttempts.
  /// </summary>
  Task<bool> MarkFailedAttemptAsync(long id, int maxAttempts, int backoffBaseSeconds, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO to avoid leaking EF entities into the Application layer.
/// </summary>
public record OutboxMessageDto
{
  public long Id { get; init; }
  public Guid MessageId { get; init; }
  public string Topic { get; init; } = string.Empty;
  public string Payload { get; init; } = string.Empty;
  public int Attempts { get; init; }
  public DateTime? DispatchedAt { get; init; }
  public DateTime? NextAttemptAt { get; init; }
  public DateTime CreatedAt { get; init; }
}
