namespace CoverLetter.Api.Models;

/// <summary>
/// Response model containing the generated cover letter.
/// </summary>
public sealed record GenerateCoverLetterResponse(
    string CoverLetter,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    DateTime GeneratedAt
);
