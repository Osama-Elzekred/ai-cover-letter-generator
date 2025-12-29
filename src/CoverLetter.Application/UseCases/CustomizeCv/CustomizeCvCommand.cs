using CoverLetter.Domain.Common;
using MediatR;

namespace CoverLetter.Application.UseCases.CustomizeCv;

/// <summary>
/// Command to customize a CV based on a job description.
/// Returns a byte array containing the generated PDF.
/// </summary>
public sealed record CustomizeCvCommand(
    string CvId,
    string JobDescription,
    bool ReturnLatexOnly = false,
    string? IdempotencyKey = null
) : IRequest<Result<CustomizeCvResult>>;
