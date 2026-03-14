using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Entities;
using CoverLetter.Domain.Enums;
using CoverLetter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoverLetter.Infrastructure.Repositories;

public class DbUserApiKeyRepository : IUserApiKeyRepository
{
  private readonly AppDbContext _context;

  public DbUserApiKeyRepository(AppDbContext context)
  {
    _context = context;
  }

  public async Task<UserApiKeyDto?> GetAsync(string userId, LlmProvider provider, CancellationToken cancellationToken = default)
  {
    var entity = await _context.UserApiKeys
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == provider, cancellationToken);

    return entity == null ? null : new UserApiKeyDto
    {
      Id = entity.Id,
      UserId = entity.UserId,
      Provider = entity.Provider,
      ApiKey = entity.ApiKey,
      CreatedAt = entity.CreatedAt,
      UpdatedAt = entity.UpdatedAt
    };
  }

  public async Task UpsertAsync(UserApiKeyDto userApiKey, CancellationToken cancellationToken = default)
  {
    var existing = await _context.UserApiKeys
      .FirstOrDefaultAsync(
        x => x.UserId == userApiKey.UserId && x.Provider == userApiKey.Provider,
        cancellationToken);

    if (existing is null)
    {
      var entity = UserApiKey.Create(userApiKey.UserId, userApiKey.Provider, userApiKey.ApiKey);
      await _context.UserApiKeys.AddAsync(entity, cancellationToken);
    }
    else
    {
      existing.Update(userApiKey.ApiKey);
    }
  }

  public async Task DeleteAsync(string userId, LlmProvider provider, CancellationToken cancellationToken = default)
  {
    await _context.UserApiKeys
      .Where(x => x.UserId == userId && x.Provider == provider)
      .ExecuteDeleteAsync(cancellationToken);
  }
}
