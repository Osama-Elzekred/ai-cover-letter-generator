using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Entities;
using CoverLetter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoverLetter.Infrastructure.Repositories;

public class DbCvRepository : Application.Repositories.ICvRepository
{
  private readonly AppDbContext _context;

  public DbCvRepository(AppDbContext context)
  {
    _context = context;
  }

  public async Task<CvDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
  {
    var entity = await _context.Cvs
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    return entity == null ? null : MapToDto(entity);
  }

  public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
  {
    return await _context.Cvs.AnyAsync(x => x.Id == id, cancellationToken);
  }

  public async Task<CvDto> AddAsync(CvDto cv, CancellationToken cancellationToken = default)
  {
    var entity = Cv.Create(cv.UserId, cv.FileName, cv.Content);
    await _context.Cvs.AddAsync(entity, cancellationToken);
    // Note: SaveChangesAsync called by handler via IUnitOfWork

    // Return DTO with domain-generated ID
    return MapToDto(entity);
  }

  public async Task UpdateAsync(CvDto cv, CancellationToken cancellationToken = default)
  {
    var entity = await _context.Cvs.FindAsync([cv.Id], cancellationToken);
    if (entity == null)
      throw new InvalidOperationException($"CV with ID {cv.Id} not found for update");

    entity.Update(cv.Content);
    // Note: SaveChangesAsync called by handler via IUnitOfWork
  }

  public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
  {
    await _context.Cvs
        .Where(x => x.Id == id)
        .ExecuteDeleteAsync(cancellationToken);
  }

  private static CvDto MapToDto(Cv entity) => new()
  {
    Id = entity.Id,
    UserId = entity.UserId,
    FileName = entity.FileName,
    Content = entity.Content,
    FileStoragePath = entity.FileStoragePath,
    IsActive = entity.IsActive,
    CreatedAt = entity.CreatedAt,
    UpdatedAt = entity.UpdatedAt
  };
}
