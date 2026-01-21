using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Entities;
using CoverLetter.Domain.Enums;
using CoverLetter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoverLetter.Infrastructure.Repositories;

public class DbUserPromptRepository : IUserPromptRepository
{
  private readonly AppDbContext _context;

  public DbUserPromptRepository(AppDbContext context)
  {
    _context = context;
  }

  public async Task<UserPromptDto?> GetAsync(Guid userId, PromptType promptType, CancellationToken cancellationToken = default)
  {
    var entity = await _context.UserPrompts
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.UserId == userId && x.PromptType == promptType, cancellationToken);

    return entity == null ? null : MapToDto(entity);
  }

  public async Task UpsertAsync(UserPromptDto prompt, CancellationToken cancellationToken = default)
  {
    var existing = await _context.UserPrompts
      .FirstOrDefaultAsync(x => x.UserId == prompt.UserId && x.PromptType == prompt.PromptType, cancellationToken);

    if (existing is null)
    {
      var entity = UserPrompt.Create(prompt.UserId, prompt.PromptType, prompt.Content);
      await _context.UserPrompts.AddAsync(entity, cancellationToken);
    }
    else
    {
      existing.Update(prompt.Content);
    }

    // Note: SaveChangesAsync called by caller via IUnitOfWork
  }

  public async Task DeleteAsync(Guid userId, PromptType promptType, CancellationToken cancellationToken = default)
  {
    await _context.UserPrompts
      .Where(x => x.UserId == userId && x.PromptType == promptType)
      .ExecuteDeleteAsync(cancellationToken);
  }

  private static UserPromptDto MapToDto(UserPrompt entity) => new()
  {
    Id = entity.Id,
    UserId = entity.UserId,
    PromptType = entity.PromptType,
    Content = entity.Content,
    CreatedAt = entity.CreatedAt,
    UpdatedAt = entity.UpdatedAt
  };
}
