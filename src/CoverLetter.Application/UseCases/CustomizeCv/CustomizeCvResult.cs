namespace CoverLetter.Application.UseCases.CustomizeCv;

/// <summary>
/// Result of the CustomizeCv use case.
/// <para>
/// When <see cref="JobId"/> is set, compilation was enqueued (HTTP 202) and the
/// caller polls <c>GET /cv/compile/status/{jobId}</c> for the result.
/// </para>
/// <para>
/// When the caller requested LaTeX only, <see cref="LatexSource"/> is populated
/// and <see cref="JobId"/> is null (HTTP 200).
/// </para>
/// </summary>
public sealed record CustomizeCvResult(
    Guid? JobId,
    string? LatexSource,
    string FileName,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    DateTime GeneratedAt
);
