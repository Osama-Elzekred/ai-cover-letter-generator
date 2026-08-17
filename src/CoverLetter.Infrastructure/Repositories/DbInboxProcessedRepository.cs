using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Entities;
using CoverLetter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoverLetter.Infrastructure.Repositories;

public class DbInboxProcessedRepository : IInboxProcessedRepository
{
  private readonly AppDbContext _context;

  public DbInboxProcessedRepository(AppDbContext context)
  {
    _context = context;
  }

  public async Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken = default)
  {
    return await _context.InboxProcessed
        .AsNoTracking()
        .AnyAsync(x => x.MessageId == messageId, cancellationToken);
  }

  public async Task AddAsync(InboxProcessedDto entry, CancellationToken cancellationToken = default)
  {
    var entity = new InboxProcessed
    {
      MessageId = entry.MessageId,
      ProcessedAt = entry.ProcessedAt,
      JobId = entry.JobId
    };
    await _context.InboxProcessed.AddAsync(entity, cancellationToken);
    // Note: SaveChangesAsync called by caller via IUnitOfWork
  }
}
