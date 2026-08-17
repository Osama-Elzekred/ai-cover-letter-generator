using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.UseCases.Compile;

/// <summary>
/// Handler for <see cref="GetCompileStatusQuery"/>.
/// Uses <see cref="ICompileJobRepository"/> for a simple get-by-id lookup.
/// </summary>
public sealed class GetCompileStatusHandler(
    ICompileJobRepository compileJobRepository,
    ILogger<GetCompileStatusHandler> logger)
    : IRequestHandler<GetCompileStatusQuery, Result<GetCompileStatusResult>>
{
    public async Task<Result<GetCompileStatusResult>> Handle(
        GetCompileStatusQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = logger.BeginScope(new Dictionary<string, object>
            {
                ["JobId"] = request.JobId,
                ["Operation"] = "GetCompileStatus"
            });

            var job = await compileJobRepository.GetByIdAsync(request.JobId, cancellationToken);

            if (job is null)
            {
                logger.LogWarning("Compile job not found: {JobId}", request.JobId);
                return Result<GetCompileStatusResult>.NotFound($"Compile job not found: {request.JobId}");
            }

            var isCompleted = job.Status == CompileJobStatus.Completed;
            var result = new GetCompileStatusResult(
                JobId: job.Id,
                Status: job.Status.ToString(),
                DownloadUrl: isCompleted ? $"/api/v1/cv/compile/result/{job.Id}" : null,
                Error: job.Error,
                CreatedAt: job.CreatedAt,
                UpdatedAt: job.UpdatedAt
            );

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve compile job status for {JobId}", request.JobId);
            return Result.Failure<GetCompileStatusResult>($"Failed to retrieve compile job status: {ex.Message}");
        }
    }
}
