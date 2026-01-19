using CoverLetter.Domain.Entities;

namespace CoverLetter.Application.UseCases.ParseCv;

/// <summary>
/// Result of CV parsing operation.
/// </summary>
public sealed record ParseCvResult(
    string CvId,
    string ExtractedText,
    string? OriginalContent,  // LaTeX source if applicable
    CvFormat Format,
    CvMetadata Metadata,
    IReadOnlyList<HyperlinkDto> Hyperlinks,
    string Preview  // First 500 characters for UI preview
)
{
  public static ParseCvResult FromDocument(CvDocument document)
  {
    var preview = document.ExtractedText.Length > 500
        ? document.ExtractedText[..500] + "..."
        : document.ExtractedText;

    var hyperlinks = document.Hyperlinks
        .Select(h => new HyperlinkDto(h.Url, h.DisplayText, h.Type))
        .ToList();

    return new ParseCvResult(
        CvId: document.Id,
        ExtractedText: document.ExtractedText,
        OriginalContent: document.OriginalContent,
        Format: document.Format,
        Metadata: document.Metadata,
        Hyperlinks: hyperlinks,
        Preview: preview
    );
  }
}

/// <summary>
/// DTO for hyperlink in API response.
/// </summary>
public sealed record HyperlinkDto(
    string Url,
    string? DisplayText,
    HyperlinkType Type
);
