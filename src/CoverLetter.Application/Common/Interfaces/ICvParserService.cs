using CoverLetter.Domain.Common;
using CoverLetter.Domain.Entities;

namespace CoverLetter.Application.Common.Interfaces;

/// <summary>
/// Service for parsing CV files and extracting text content.
/// Abstraction enables multiple parser implementations (PDF, LaTeX, PlainText).
/// Infrastructure layer implements this interface.
/// </summary>
public interface ICvParserService
{
  /// <summary>
  /// Parses CV file content and extracts structured information.
  /// </summary>
  /// <param name="fileName">Original file name (used for logging/errors)</param>
  /// <param name="fileContent">Raw file bytes</param>
  /// <param name="format">CV format (PDF, LaTeX, PlainText)</param>
  /// <param name="cancellationToken">Cancellation token</param>
  /// <returns>Result containing parsed CvDocument or error</returns>
  Task<Result<CvDocument>> ParseAsync(
      string fileName,
      byte[] fileContent,
      CvFormat format,
      CancellationToken cancellationToken = default);
}
