using CoverLetter.Domain.Common;
using MediatR;

namespace CoverLetter.Application.UseCases.Compile;

/// <summary>
/// Query to poll the status of a compile job.
/// </summary>
public sealed record GetCompileStatusQuery(Guid JobId) : IRequest<Result<GetCompileStatusResult>>;

/// <summary>
/// Current status of a compile job.
/// <see cref="DownloadUrl"/> is populated only when the job has completed.
/// </summary>
public sealed record GetCompileStatusResult(
    Guid JobId,
    string Status,
    string? DownloadUrl,
    string? Error,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
