using CoverLetter.Domain.Enums;

namespace CoverLetter.Application.Repositories;

public interface IUserPromptRepository
{
  Task<UserPromptDto?> GetAsync(Guid userId, PromptType promptType, CancellationToken cancellationToken = default);
  Task UpsertAsync(UserPromptDto prompt, CancellationToken cancellationToken = default);
  Task DeleteAsync(Guid userId, PromptType promptType, CancellationToken cancellationToken = default);
}

public record UserPromptDto
{
  public Guid Id { get; init; }
  public Guid UserId { get; init; }
  public PromptType PromptType { get; init; }
  public string Content { get; init; } = string.Empty;
  public DateTime CreatedAt { get; init; }
  public DateTime UpdatedAt { get; init; }
}
