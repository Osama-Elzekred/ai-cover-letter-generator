using CoverLetter.Domain.Enums;

namespace CoverLetter.Application.Repositories;

public interface IUserApiKeyRepository
{
  Task<UserApiKeyDto?> GetAsync(string userId, LlmProvider provider, CancellationToken cancellationToken = default);
  Task UpsertAsync(UserApiKeyDto userApiKey, CancellationToken cancellationToken = default);
  Task DeleteAsync(string userId, LlmProvider provider, CancellationToken cancellationToken = default);
}

public record UserApiKeyDto
{
  public Guid Id { get; init; }
  public string UserId { get; init; } = string.Empty;
  public LlmProvider Provider { get; init; }
  public string ApiKey { get; init; } = string.Empty;
  public DateTime CreatedAt { get; init; }
  public DateTime UpdatedAt { get; init; }
}
