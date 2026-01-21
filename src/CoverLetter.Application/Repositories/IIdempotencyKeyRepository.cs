namespace CoverLetter.Application.Repositories;

/// <summary>
/// Repository for idempotency key storage - ensures exactly-once processing
/// </summary>
public interface IIdempotencyKeyRepository
{
  Task<IdempotencyResultDto?> GetByKeyAsync(string key, Guid userId, CancellationToken cancellationToken = default);
  Task StoreAsync(IdempotencyResultDto result, CancellationToken cancellationToken = default);
  Task CleanupExpiredAsync(CancellationToken cancellationToken = default);
}

public record IdempotencyResultDto
{
  public Guid Id { get; init; }
  public string Key { get; init; } = string.Empty;
  public Guid UserId { get; init; }
  public string RequestPath { get; init; } = string.Empty;
  public int StatusCode { get; init; }
  public string? ResponseBody { get; init; }
  public DateTime CreatedAt { get; init; }
  public DateTime ExpiresAt { get; init; }
}
