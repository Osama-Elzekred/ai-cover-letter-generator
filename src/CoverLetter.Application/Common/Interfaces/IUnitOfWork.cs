namespace CoverLetter.Application.Common.Interfaces;

/// <summary>
/// Unit of Work pattern for managing database transactions.
/// Handlers call SaveChangesAsync() after repository operations.
/// </summary>
public interface IUnitOfWork
{
  Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
