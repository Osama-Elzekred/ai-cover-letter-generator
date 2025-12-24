namespace CoverLetter.Api.Endpoints;

/// <summary>
/// Request DTOs for CV endpoints.
/// </summary>
public static partial class CvEndpoints
{
  /// <summary>
  /// Form model for CV file upload and parsing.
  /// </summary>
  public sealed class ParseCvForm
  {
    /// <summary>
    /// The CV file to upload (PDF, LaTeX, or plain text).
    /// </summary>
    public IFormFile File { get; init; } = default!;

    /// <summary>
    /// Optional CV format: "pdf", "latex", or "plaintext".
    /// If not specified, format will be determined from file extension.
    /// </summary>
    public string? Format { get; init; }

  }
}
