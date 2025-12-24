using CoverLetter.Application.Common.Behaviors;
using CoverLetter.Domain.Common;
using MediatR;

namespace CoverLetter.Application.UseCases.GenerateCoverLetter;

/// <summary>
/// Command to generate a cover letter.
/// CQRS: This is a Command (changes/creates something).
/// Supports idempotency via IdempotencyKey.
/// Accepts either CvId (reference to cached CV) OR CvText (direct input).
/// User identification handled via IUserContext (injected in handler).
/// </summary>
public sealed record GenerateCoverLetterCommand(
    string JobDescription,
    string? CvId = null,              // Cached CV reference
    string? CvText = null,            // Direct CV text (backward compatibility)
    string? CustomPromptTemplate = null,
    PromptMode PromptMode = PromptMode.Append,  // Default: append to default prompt
    string? IdempotencyKey = null
) : IRequest<Result<GenerateCoverLetterResult>>, IIdempotentRequest
{
    string? IIdempotentRequest.IdempotencyKey => IdempotencyKey;
}
