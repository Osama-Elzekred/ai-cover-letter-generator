using CoverLetter.Domain.Common;
using MediatR;

namespace CoverLetter.Application.UseCases.GenerateCoverLetter;

/// <summary>
/// Command to generate a cover letter.
/// CQRS: This is a Command (changes/creates something).
/// </summary>
public sealed record GenerateCoverLetterCommand(
    string JobDescription,
    string CvText,
    string? CustomPromptTemplate = null
) : IRequest<Result<GenerateCoverLetterResult>>;
