using CoverLetter.Application.UseCases.GenerateCoverLetter;
using CoverLetter.Domain.Common;
using MediatR;

namespace CoverLetter.Application.UseCases.CustomizeCv;

/// <summary>
/// Command to customize a CV based on a job description.
/// The LLM generates LaTeX synchronously, then compilation is enqueued and the
/// job id is returned (HTTP 202). When <see cref="ReturnLatexOnly"/> is set the
/// LaTeX source is returned directly without enqueuing a compile (HTTP 200).
/// Idempotency is DB-backed, so this command does not implement IIdempotentRequest.
/// </summary>
public sealed record CustomizeCvCommand(
    Guid CvId,
    string JobDescription,
    string? CustomPromptTemplate = null,
    PromptMode PromptMode = PromptMode.Append,
    IEnumerable<string>? SelectedKeywords = null,
    bool ReturnLatexOnly = false,
    string? IdempotencyKey = null
) : IRequest<Result<CustomizeCvResult>>;
