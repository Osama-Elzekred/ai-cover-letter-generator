using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Entities;
using CoverLetter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoverLetter.Infrastructure.Repositories;

public class DbOutboxMessageRepository : IOutboxMessageRepository
{
  private readonly AppDbContext _context;

  public DbOutboxMessageRepository(AppDbContext context)
  {
    _context = context;
  }

  public async Task AddAsync(OutboxMessageDto message, CancellationToken cancellationToken = default)
  {
    var entity = new OutboxMessage
    {
      MessageId = message.MessageId,
      Topic = message.Topic,
      Payload = message.Payload,
      Attempts = 0,
      CreatedAt = message.CreatedAt
    };
    await _context.OutboxMessages.AddAsync(entity, cancellationToken);
    // Note: SaveChangesAsync called by caller via IUnitOfWork
  }

  public async Task<IReadOnlyList<OutboxMessageDto>> GetUndispatchedBatchAsync(
      int batchSize,
      CancellationToken cancellationToken = default)
  {
    var now = DateTime.UtcNow;

    var rows = await _context.OutboxMessages
        .AsNoTracking()
        .Where(x => x.DispatchedAt == null
            && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
        .OrderBy(x => x.Id)
        .Take(batchSize)
        .ToListAsync(cancellationToken);

    return rows.Select(MapToDto).ToList();
  }

  public async Task MarkDispatchedAsync(long id, CancellationToken cancellationToken = default)
  {
    await _context.OutboxMessages
        .Where(x => x.Id == id)
        .ExecuteUpdateAsync(s => s
            .SetProperty(x => x.DispatchedAt, DateTime.UtcNow), cancellationToken);
  }

  public async Task<bool> MarkFailedAttemptAsync(
      long id,
      int maxAttempts,
      int backoffBaseSeconds,
      CancellationToken cancellationToken = default)
  {
    // Fetch current attempts atomically and decide on backoff.
    var row = await _context.OutboxMessages
        .Where(x => x.Id == id)
        .Select(x => new { x.Attempts })
        .FirstOrDefaultAsync(cancellationToken);

    if (row is null)
      return false;

    var nextAttempts = row.Attempts + 1;
    var exhausted = nextAttempts > maxAttempts;
    // Exponential backoff: base * 2^(attempts-1)
    var delay = TimeSpan.FromSeconds(backoffBaseSeconds * Math.Pow(2, Math.Max(0, nextAttempts - 1)));
    var nextAttemptAt = exhausted ? (DateTime?)null : DateTime.UtcNow + delay;

    await _context.OutboxMessages
        .Where(x => x.Id == id)
        .ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Attempts, nextAttempts)
            .SetProperty(x => x.NextAttemptAt, nextAttemptAt), cancellationToken);

    return !exhausted;
  }

  private static OutboxMessageDto MapToDto(OutboxMessage entity) => new()
  {
    Id = entity.Id,
    MessageId = entity.MessageId,
    Topic = entity.Topic,
    Payload = entity.Payload,
    Attempts = entity.Attempts,
    DispatchedAt = entity.DispatchedAt,
    NextAttemptAt = entity.NextAttemptAt,
    CreatedAt = entity.CreatedAt
  };
}
