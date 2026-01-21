using CoverLetter.Domain.Entities;

namespace CoverLetter.Application.Repositories;

/// <summary>
/// Repository for CoverLetter write-heavy aggregate.
/// Commands use this; Queries use IQueryContext directly with IQueryable.
/// Returns rich domain entities or throws - handlers wrap in Result<T>.
/// </summary>
public interface ICoverLetterRepository
{
  /// <summary>
  /// Get a cover letter by ID. Returns null if not found.
  /// </summary>
  Task<CoverLetterEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

  /// <summary>
  /// Check if a cover letter exists.
  /// </summary>
  Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

  /// <summary>
  /// Add a new cover letter. Handler manages SaveChangesAsync.
  /// </summary>
  Task AddAsync(CoverLetterEntity letter, CancellationToken cancellationToken = default);

  /// <summary>
  /// Update an existing cover letter. Throws if not found. Handler manages SaveChangesAsync.
  /// </summary>
  Task UpdateAsync(CoverLetterEntity letter, CancellationToken cancellationToken = default);

  /// <summary>
  /// Delete a cover letter. Handler manages SaveChangesAsync.
  /// </summary>
  Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
