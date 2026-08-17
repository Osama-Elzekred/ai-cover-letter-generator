using CoverLetter.Domain.Common;

namespace CoverLetter.Application.Common.Interfaces;

/// <summary>
/// Orchestrates transactional enqueue of a compile job:
/// creates a <c>CompileJob</c> row and a matching <c>OutboxMessage</c> in a single
/// DB transaction so that a successful API response guarantees the job will be dispatched.
/// Handles DB-backed idempotency so duplicate enqueue requests for the same
/// (userId, idempotencyKey) return the existing job id.
/// </summary>
public interface ICompileJobEnqueuer
{
  /// <summary>
  /// Enqueues a LaTeX compile job.
  /// </summary>
  /// <returns>The job id and a polling status url, or a failure result.</returns>
  Task<Result<CompileJobEnqueueResult>> EnqueueAsync(
      string? userId,
      string? idempotencyKey,
      string latexSource,
      CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a successful enqueue.
/// </summary>
public sealed record CompileJobEnqueueResult(Guid JobId);
