using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Entities;
using CoverLetter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoverLetter.Infrastructure.Repositories;

/// <summary>
/// Repository for CoverLetter aggregate (write operations).
/// Returns rich domain entities or throws exceptions.
/// Handlers wrap in Result<T> - this is infrastructure concern only.
/// Note: SaveChangesAsync managed by handlers/behaviors, not here.
/// </summary>
public class CoverLetterRepository : ICoverLetterRepository
{
    private readonly AppDbContext _context;

    public CoverLetterRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CoverLetterEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CoverLetters
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CoverLetters.AnyAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(CoverLetterEntity letter, CancellationToken cancellationToken = default)
    {
        await _context.CoverLetters.AddAsync(letter, cancellationToken);
        // Note: SaveChangesAsync is NOT called here; handler/behavior manages it
    }

    public async Task UpdateAsync(CoverLetterEntity letter, CancellationToken cancellationToken = default)
    {
        var existing = await _context.CoverLetters
            .FirstOrDefaultAsync(x => x.Id == letter.Id, cancellationToken);

        if (existing == null)
            throw new InvalidOperationException($"CoverLetter with ID {letter.Id} not found");

        existing.Content = letter.Content;
        existing.Status = letter.Status;
        existing.UpdatedAt = DateTime.UtcNow;
        if (letter.PublishedAt.HasValue)
            existing.PublishedAt = letter.PublishedAt;

        // Note: SaveChangesAsync is NOT called here; handler/behavior manages it
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _context.CoverLetters
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        // Note: SaveChangesAsync is NOT called here; handler/behavior manages it
    }
}
