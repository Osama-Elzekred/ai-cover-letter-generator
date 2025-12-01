using CoverLetter.Api.Models;

namespace CoverLetter.Api.Services;

/// <summary>
/// Interface for cover letter generation service.
/// </summary>
public interface ICoverLetterService
{
  /// <summary>
  /// Generates a cover letter based on job description and CV.
  /// </summary>
  Task<GenerateCoverLetterResponse> GenerateAsync(
      GenerateCoverLetterRequest request,
      CancellationToken cancellationToken = default);
}
