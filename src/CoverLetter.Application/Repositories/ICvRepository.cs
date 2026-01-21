namespace CoverLetter.Application.Repositories;

/// <summary>
/// Repository for CV aggregate - keeps persistence details in Infrastructure
/// </summary>
public interface ICvRepository
{
  Task<CvDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
  Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
  /// <summary>
  /// Adds a new CV and returns the entity with generated ID.
  /// Domain entity generates its own identity.
  /// </summary>
  Task<CvDto> AddAsync(CvDto cv, CancellationToken cancellationToken = default);
  Task UpdateAsync(CvDto cv, CancellationToken cancellationToken = default);
  Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO to avoid leaking EF entities into Application layer
/// </summary>
public record CvDto
{
  public Guid Id { get; init; }
  public Guid UserId { get; init; }
  public string FileName { get; init; } = string.Empty;
  public string Content { get; init; } = string.Empty;
  public string? FileStoragePath { get; init; }
  public bool IsActive { get; init; }
  public DateTime CreatedAt { get; init; }
  public DateTime UpdatedAt { get; init; }
}
