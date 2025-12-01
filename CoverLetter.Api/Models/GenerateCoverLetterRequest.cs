namespace CoverLetter.Api.Models;

/// <summary>
/// Request model for generating a cover letter.
/// </summary>
public sealed record GenerateCoverLetterRequest(
    string JobDescription,
    string CvText,
    string? CustomPromptTemplate = null
)
{
  /// <summary>
  /// Validates the request and returns validation errors if any.
  /// </summary>
  public IEnumerable<string> Validate()
  {
    if (string.IsNullOrWhiteSpace(JobDescription))
      yield return "Job description is required.";

    if (string.IsNullOrWhiteSpace(CvText))
      yield return "CV text is required.";

    if (JobDescription?.Length > 50000)
      yield return "Job description exceeds maximum length of 50,000 characters.";

    if (CvText?.Length > 50000)
      yield return "CV text exceeds maximum length of 50,000 characters.";
  }
}
