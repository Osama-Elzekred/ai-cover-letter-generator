namespace CoverLetter.Domain.Entities;

/// <summary>
/// Represents a parsed CV document.
/// Domain entity for CV storage and management.
/// </summary>
public sealed class CvDocument
{
  public string Id { get; init; }
  public string FileName { get; init; }
  public CvFormat Format { get; init; }
  public string ExtractedText { get; init; }
  public string? OriginalContent { get; init; }  // For LaTeX source
  public IReadOnlyList<Hyperlink> Hyperlinks { get; init; }
  public CvMetadata Metadata { get; init; }
  public DateTime UploadedAt { get; init; }

  private CvDocument(
      string id,
      string fileName,
      CvFormat format,
      string extractedText,
      string? originalContent,
      IReadOnlyList<Hyperlink> hyperlinks,
      CvMetadata metadata)
  {
    Id = id;
    FileName = fileName;
    Format = format;
    ExtractedText = extractedText;
    OriginalContent = originalContent;
    Hyperlinks = hyperlinks;
    Metadata = metadata;
    UploadedAt = DateTime.UtcNow;
  }

  public static CvDocument Create(
      string fileName,
      CvFormat format,
      string extractedText,
      string? originalContent = null,
      IReadOnlyList<Hyperlink>? hyperlinks = null,
      CvMetadata? metadata = null)
  {
    return new CvDocument(
        id: Guid.NewGuid().ToString("N"),
        fileName: fileName,
        format: format,
        extractedText: extractedText,
        originalContent: originalContent,
        hyperlinks: hyperlinks ?? Array.Empty<Hyperlink>(),
        metadata: metadata ?? CvMetadata.Empty
    );
  }
}

/// <summary>
/// Supported CV formats.
/// </summary>
public enum CvFormat
{
  Pdf,
  LaTeX,
  PlainText
}

/// <summary>
/// Metadata extracted from CV.
/// </summary>
public sealed record CvMetadata(
    int PageCount,
    long FileSizeBytes,
    int CharacterCount,
    int WordCount
)
{
  public static CvMetadata Empty => new(0, 0, 0, 0);

  public static CvMetadata FromText(string text, long fileSize = 0, int pageCount = 1)
  {
    var charCount = text.Length;
    var wordCount = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

    return new CvMetadata(pageCount, fileSize, charCount, wordCount);
  }
}

/// <summary>
/// Represents a hyperlink extracted from the CV.
/// </summary>
public sealed record Hyperlink(
    string Url,
    string? DisplayText = null,
    HyperlinkType Type = HyperlinkType.General
);

/// <summary>
/// Categorization of hyperlink types for context.
/// </summary>
public enum HyperlinkType
{
  General,
  Email,
  LinkedIn,
  GitHub,
  Portfolio,
  Other
}
