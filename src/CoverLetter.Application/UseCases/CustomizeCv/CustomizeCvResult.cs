namespace CoverLetter.Application.UseCases.CustomizeCv;

/// <summary>
/// Result of the CustomizeCv use case.
/// </summary>
public sealed record CustomizeCvResult(
    byte[]? PdfContent,
    string? LatexSource,
    string FileName,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    DateTime GeneratedAt
);
