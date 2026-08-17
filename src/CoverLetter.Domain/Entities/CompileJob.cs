using CoverLetter.Domain.Enums;

namespace CoverLetter.Domain.Entities;

/// <summary>
/// Represents a LaTeX compile job tracked through the queue + worker pipeline.
/// Rich aggregate root: identity and state transitions are owned by the domain.
/// </summary>
public class CompileJob
{
  private CompileJob() { } // EF Core constructor

  public Guid Id { get; private set; }
  public CompileJobStatus Status { get; private set; }
  public string? UserId { get; private set; }
  public string? IdempotencyKey { get; private set; }
  public string? ResultPath { get; private set; }
  public string? Error { get; private set; }
  public DateTime CreatedAt { get; private set; }
  public DateTime UpdatedAt { get; private set; }

  /// <summary>
  /// Factory method to create a new pending compile job.
  /// Domain owns identity generation - ID is created here, not by the caller.
  /// </summary>
  public static CompileJob Create(string? userId, string? idempotencyKey)
  {
    var now = DateTime.UtcNow;
    return new CompileJob
    {
      Id = Guid.NewGuid(),
      Status = CompileJobStatus.Pending,
      UserId = userId,
      IdempotencyKey = idempotencyKey,
      CreatedAt = now,
      UpdatedAt = now
    };
  }

  /// <summary>
  /// Marks the job as being actively compiled by a worker.
  /// Only valid from the Pending state.
  /// </summary>
  public void MarkProcessing()
  {
    if (Status != CompileJobStatus.Pending)
      throw new InvalidOperationException(
        $"Cannot transition to Processing from {Status}.");

    Status = CompileJobStatus.Processing;
    UpdatedAt = DateTime.UtcNow;
  }

  /// <summary>
  /// Marks the job as completed and records the path to the resulting PDF.
  /// </summary>
  public void MarkCompleted(string resultPath)
  {
    if (string.IsNullOrWhiteSpace(resultPath))
      throw new ArgumentException("Result path is required", nameof(resultPath));

    Status = CompileJobStatus.Completed;
    ResultPath = resultPath;
    Error = null;
    UpdatedAt = DateTime.UtcNow;
  }

  /// <summary>
  /// Marks the job as failed and records the failure reason.
  /// </summary>
  public void MarkFailed(string error)
  {
    if (string.IsNullOrWhiteSpace(error))
      throw new ArgumentException("Error reason is required", nameof(error));

    Status = CompileJobStatus.Failed;
    Error = error;
    UpdatedAt = DateTime.UtcNow;
  }

  /// <summary>
  /// Cancels a pending job. Only valid from the Pending state.
  /// </summary>
  public void Cancel()
  {
    if (Status != CompileJobStatus.Pending)
      throw new InvalidOperationException(
        $"Cannot cancel a job in {Status} state.");

    Status = CompileJobStatus.Cancelled;
    UpdatedAt = DateTime.UtcNow;
  }
}
