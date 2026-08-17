using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Entities;
using CoverLetter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoverLetter.Infrastructure.Repositories;

public class DbCompileJobRepository : Application.Repositories.ICompileJobRepository
{
  private readonly AppDbContext _context;

  public DbCompileJobRepository(AppDbContext context)
  {
    _context = context;
  }

  public async Task<CompileJobDto> AddAsync(CompileJobDto job, CancellationToken cancellationToken = default)
  {
    var entity = CompileJob.Create(job.UserId, job.IdempotencyKey);
    await _context.CompileJobs.AddAsync(entity, cancellationToken);
    // Note: SaveChangesAsync called by caller via IUnitOfWork
    return MapToDto(entity);
  }

  public async Task<CompileJobDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
  {
    var entity = await _context.CompileJobs
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    return entity is null ? null : MapToDto(entity);
  }

  public async Task<CompileJobDto?> FindByIdempotencyKeyAsync(
      string userId,
      string idempotencyKey,
      CancellationToken cancellationToken = default)
  {
    var entity = await _context.CompileJobs
        .AsNoTracking()
        .Where(x => x.UserId == userId && x.IdempotencyKey == idempotencyKey)
        .OrderByDescending(x => x.CreatedAt)
        .FirstOrDefaultAsync(cancellationToken);

    return entity is null ? null : MapToDto(entity);
  }

  public async Task MarkProcessingAsync(Guid jobId, CancellationToken cancellationToken = default)
  {
    var entity = await _context.CompileJobs.FindAsync([jobId], cancellationToken)
        ?? throw new InvalidOperationException($"Compile job {jobId} not found.");
    entity.MarkProcessing();
    // Note: SaveChangesAsync called by caller via IUnitOfWork
  }

  public async Task MarkCompletedAsync(Guid jobId, string resultPath, CancellationToken cancellationToken = default)
  {
    var entity = await _context.CompileJobs.FindAsync([jobId], cancellationToken)
        ?? throw new InvalidOperationException($"Compile job {jobId} not found.");
    entity.MarkCompleted(resultPath);
    // Note: SaveChangesAsync called by caller via IUnitOfWork
  }

  public async Task MarkFailedAsync(Guid jobId, string error, CancellationToken cancellationToken = default)
  {
    var entity = await _context.CompileJobs.FindAsync([jobId], cancellationToken)
        ?? throw new InvalidOperationException($"Compile job {jobId} not found.");
    entity.MarkFailed(error);
    // Note: SaveChangesAsync called by caller via IUnitOfWork
  }

  private static CompileJobDto MapToDto(CompileJob entity) => new()
  {
    Id = entity.Id,
    Status = entity.Status,
    UserId = entity.UserId,
    IdempotencyKey = entity.IdempotencyKey,
    ResultPath = entity.ResultPath,
    Error = entity.Error,
    CreatedAt = entity.CreatedAt,
    UpdatedAt = entity.UpdatedAt
  };
}
