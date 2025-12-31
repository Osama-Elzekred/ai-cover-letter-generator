using CoverLetter.Domain.Common;
using CoverLetter.Domain.Entities;

namespace CoverLetter.Application.Common.Interfaces;

/// <summary>
/// Repository for managing the lifecycle of parsed CV documents.
/// Abstracts the underlying storage (MemoryCache).
/// </summary>
public interface ICvRepository
{
    Task<Result<CvDocument>> GetByIdAsync(string cvId, CancellationToken cancellationToken = default);
    Task SaveAsync(CvDocument document, CancellationToken cancellationToken = default);
    Task DeleteAsync(string cvId, CancellationToken cancellationToken = default);
}
