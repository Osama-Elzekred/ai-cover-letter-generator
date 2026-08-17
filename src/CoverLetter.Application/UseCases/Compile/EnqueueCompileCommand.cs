using CoverLetter.Domain.Common;
using MediatR;

namespace CoverLetter.Application.UseCases.Compile;

/// <summary>
/// Command to enqueue a LaTeX compile job.
/// Returns the job id so the caller can poll <c>GET /cv/compile/status/{jobId}</c>.
/// Idempotency is DB-backed (not via the in-memory cache) so it scales horizontally.
/// </summary>
public sealed record EnqueueCompileCommand(
    string LatexSource,
    string? IdempotencyKey = null
) : IRequest<Result<EnqueueCompileResult>>;

/// <summary>
/// Result returned for an enqueued compile job (HTTP 202 Accepted).
/// </summary>
public sealed record EnqueueCompileResult(Guid JobId);
