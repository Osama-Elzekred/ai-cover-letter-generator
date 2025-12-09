using CoverLetter.Application.Common.Behaviors;
using CoverLetter.Domain.Common;
using MediatR;

namespace CoverLetter.Application.UseCases.GenerateCoverLetter;

/// <summary>
/// Command to generate a cover letter.
/// CQRS: This is a Command (changes/creates something).
/// Supports idempotency via IdempotencyKey.
/// </summary>
public sealed record GenerateCoverLetterCommand(
    string JobDescription,
    string CvText,
    string? CustomPromptTemplate = null,
    string? IdempotencyKey = null
) : IRequest<Result<GenerateCoverLetterResult>>, IIdempotentRequest;
