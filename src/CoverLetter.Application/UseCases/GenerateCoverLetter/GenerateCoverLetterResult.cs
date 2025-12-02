namespace CoverLetter.Application.UseCases.GenerateCoverLetter;

/// <summary>
/// Result of cover letter generation.
/// </summary>
public sealed record GenerateCoverLetterResult(
    string CoverLetter,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    DateTime GeneratedAt
);
