using CoverLetter.Application.Common.Extensions;
using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.UseCases.Compile;

/// <summary>
/// Handler for <see cref="EnqueueCompileCommand"/>.
/// Delegates transactional job + outbox creation to <see cref="ICompileJobEnqueuer"/>.
/// </summary>
public sealed class EnqueueCompileHandler(
    ICompileJobEnqueuer enqueuer,
    IUserContext userContext,
    ILogger<EnqueueCompileHandler> logger)
    : IRequestHandler<EnqueueCompileCommand, Result<EnqueueCompileResult>>
{
    public async Task<Result<EnqueueCompileResult>> Handle(
        EnqueueCompileCommand request,
        CancellationToken cancellationToken)
    {
        using var scope = logger.BeginHandlerScope(userContext, "EnqueueCompile", new()
        {
            ["HasIdempotencyKey"] = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
        });

        var result = await enqueuer.EnqueueAsync(
            userContext.UserId,
            request.IdempotencyKey,
            request.LatexSource,
            cancellationToken);

        if (result.IsFailure)
            return Result<EnqueueCompileResult>.Failure(result.Errors, result.Type);

        return Result.Success(new EnqueueCompileResult(result.Value.JobId));
    }
}
