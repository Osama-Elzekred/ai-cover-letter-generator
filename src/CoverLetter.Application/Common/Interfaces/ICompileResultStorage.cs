namespace CoverLetter.Application.Common.Interfaces;

/// <summary>
/// Persists compiled PDF results and reads them back for download.
/// Abstraction over the physical storage location (local volume in dev,
/// cloud blob in production) so the API and worker depend on a port, not a path.
/// </summary>
public interface ICompileResultStorage
{
  /// <summary>
  /// Writes the PDF bytes for a job and returns the stored result path/identifier.
  /// </summary>
  Task<string> WriteAsync(Guid jobId, byte[] content, CancellationToken cancellationToken = default);

  /// <summary>
  /// Reads the PDF bytes for a job, or null if no result is stored yet.
  /// </summary>
  Task<byte[]?> ReadAsync(Guid jobId, CancellationToken cancellationToken = default);
}
