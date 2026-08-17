using CoverLetter.Domain.Enums;

namespace CoverLetter.Application.Repositories;

/// <summary>
/// Repository for the CompileJob aggregate.
/// Persists compile job state transitions through the queue + worker pipeline.
/// </summary>
public interface ICompileJobRepository
{
  /// <summary>
  /// Adds a new pending compile job. SaveChangesAsync is invoked by the caller via IUnitOfWork
  /// so the job and its outbox message are committed in a single transaction.
  /// </summary>
  Task<CompileJobDto> AddAsync(CompileJobDto job, CancellationToken cancellationToken = default);

  /// <summary>
  /// Retrieves a compile job by id (read-only projection).
  /// </summary>
  Task<CompileJobDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

  /// <summary>
  /// Finds an existing job for the given user + idempotency key.
  /// Used to make enqueue idempotent without relying on in-memory caching.
  /// </summary>
  Task<CompileJobDto?> FindByIdempotencyKeyAsync(string userId, string idempotencyKey, CancellationToken cancellationToken = default);

  /// <summary>
  /// Transitions a job to the Processing state. SaveChangesAsync called via IUnitOfWork.
  /// </summary>
  Task MarkProcessingAsync(Guid jobId, CancellationToken cancellationToken = default);

  /// <summary>
  /// Transitions a job to Completed and records the PDF result path.
  /// SaveChangesAsync called via IUnitOfWork.
  /// </summary>
  Task MarkCompletedAsync(Guid jobId, string resultPath, CancellationToken cancellationToken = default);

  /// <summary>
  /// Transitions a job to Failed and records the error reason.
  /// SaveChangesAsync called via IUnitOfWork.
  /// </summary>
  Task MarkFailedAsync(Guid jobId, string error, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO to avoid leaking EF entities into the Application layer.
/// </summary>
public record CompileJobDto
{
  public Guid Id { get; init; }
  public CompileJobStatus Status { get; init; }
  public string? UserId { get; init; }
  public string? IdempotencyKey { get; init; }
  public string? ResultPath { get; init; }
  public string? Error { get; init; }
  public DateTime CreatedAt { get; init; }
  public DateTime UpdatedAt { get; init; }
}
