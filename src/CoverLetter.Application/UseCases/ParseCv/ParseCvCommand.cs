using CoverLetter.Application.Common.Behaviors;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Entities;
using MediatR;

namespace CoverLetter.Application.UseCases.ParseCv;

/// <summary>
/// Command to parse a CV file and extract text content.
/// Supports PDF and LaTeX formats.
/// Supports idempotency via IdempotencyKey.
/// </summary>
public sealed record ParseCvCommand(
    string FileName,
    byte[] FileContent,
    CvFormat Format,
    string? IdempotencyKey = null
) : IRequest<Result<ParseCvResult>>, IIdempotentRequest
{
    // IIdempotentRequest implementation
    string? IIdempotentRequest.IdempotencyKey => IdempotencyKey;
}
