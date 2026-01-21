using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Entities;
using CoverLetter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoverLetter.Infrastructure.Repositories;

public class DbIdempotencyKeyRepository : Application.Repositories.IIdempotencyKeyRepository
{
    private readonly AppDbContext _context;

    public DbIdempotencyKeyRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IdempotencyResultDto?> GetByKeyAsync(
        string key,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key && x.UserId == userId, cancellationToken);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task StoreAsync(IdempotencyResultDto result, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(result);
        await _context.IdempotencyKeys.AddAsync(entity, cancellationToken);
        // Note: SaveChangesAsync called by handler via IUnitOfWork
    }

    public async Task CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        await _context.IdempotencyKeys
            .Where(x => x.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static IdempotencyResultDto MapToDto(IdempotencyKey entity) => new()
    {
        Id = entity.Id,
        Key = entity.Key,
        UserId = entity.UserId,
        RequestPath = entity.RequestPath,
        StatusCode = entity.StatusCode,
        ResponseBody = entity.ResponseBody,
        CreatedAt = entity.CreatedAt,
        ExpiresAt = entity.ExpiresAt
    };

    private static IdempotencyKey MapToEntity(IdempotencyResultDto dto) => new()
    {
        Id = dto.Id,
        Key = dto.Key,
        UserId = dto.UserId,
        RequestPath = dto.RequestPath,
        StatusCode = dto.StatusCode,
        ResponseBody = dto.ResponseBody,
        CreatedAt = dto.CreatedAt,
        ExpiresAt = dto.ExpiresAt
    };
}
