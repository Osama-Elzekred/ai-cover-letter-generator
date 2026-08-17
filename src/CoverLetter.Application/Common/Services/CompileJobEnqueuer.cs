using System.Text.Json;
using CoverLetter.Application.Common.Extensions;
using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.Common.Services;

/// <summary>
/// Transactional enqueue of a compile job: creates a <c>CompileJob</c> row and a
/// matching <c>OutboxMessage</c> in a single DB transaction, so a successful API
/// response guarantees the job will be dispatched. DB-backed idempotency makes
/// duplicate enqueue requests for the same (userId, idempotencyKey) return the
/// existing job id without relying on in-memory caching.
/// </summary>
public sealed class CompileJobEnqueuer(
    ICompileJobRepository compileJobRepository,
    IOutboxMessageRepository outboxMessageRepository,
    IUnitOfWork unitOfWork,
    ILogger<CompileJobEnqueuer> logger) : ICompileJobEnqueuer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<CompileJobEnqueueResult>> EnqueueAsync(
        string? userId,
        string? idempotencyKey,
        string latexSource,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(latexSource))
            return Result<CompileJobEnqueueResult>.ValidationError("LaTeX source is required.");

        // Idempotency replay: if a job already exists for this (user, key), return it.
        if (!string.IsNullOrWhiteSpace(idempotencyKey) && !string.IsNullOrWhiteSpace(userId))
        {
            var existing = await compileJobRepository.FindByIdempotencyKeyAsync(userId, idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                logger.LogInformation("Idempotent enqueue replay returning existing job {JobId}", existing.Id);
                return Result.Success(new CompileJobEnqueueResult(existing.Id));
            }
        }

        var now = DateTime.UtcNow;

        // 1. Create the compile job (domain generates the id).
        var job = await compileJobRepository.AddAsync(new CompileJobDto
        {
            UserId = userId,
            IdempotencyKey = idempotencyKey,
            Status = CompileJobStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);

        // 2. Build the message payload referencing the job id.
        var message = new CompileJobMessage
        {
            JobId = job.Id,
            UserId = userId,
            IdempotencyKey = idempotencyKey,
            Latex = latexSource,
            Options = new CompileOptions()
        };
        var payload = JsonSerializer.Serialize(message, JsonOptions);

        // 3. Create the outbox message in the same transaction.
        await outboxMessageRepository.AddAsync(new OutboxMessageDto
        {
            MessageId = Guid.NewGuid(),
            Topic = CompileTopics.LatexCompile,
            Payload = payload,
            Attempts = 0,
            CreatedAt = now
        }, cancellationToken);

        // 4. Commit both rows atomically.
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (!string.IsNullOrWhiteSpace(idempotencyKey) && !string.IsNullOrWhiteSpace(userId))
        {
            // Concurrent enqueue with the same idempotency key raced on the unique index.
            // Re-query the winning job and return it as an idempotent replay.
            var winner = await compileJobRepository.FindByIdempotencyKeyAsync(userId, idempotencyKey, cancellationToken);
            if (winner is not null)
            {
                logger.LogInformation(ex, "Idempotent enqueue race resolved to existing job {JobId}", winner.Id);
                return Result.Success(new CompileJobEnqueueResult(winner.Id));
            }

            throw;
        }

        logger.LogInformation("Enqueued compile job {JobId}", job.Id);
        return Result.Success(new CompileJobEnqueueResult(job.Id));
    }
}

/// <summary>
/// Topic constants for outbox messages.
/// </summary>
public static class CompileTopics
{
    public const string LatexCompile = "compile";
}
