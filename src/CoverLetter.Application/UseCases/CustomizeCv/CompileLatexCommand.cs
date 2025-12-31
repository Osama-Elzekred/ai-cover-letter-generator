using CoverLetter.Application.Common.Behaviors;
using CoverLetter.Domain.Common;
using MediatR;

namespace CoverLetter.Application.UseCases.CustomizeCv;

/// <summary>
/// Command to compile raw LaTeX into a PDF.
/// </summary>
public sealed record CompileLatexCommand(
    string LatexSource,
    string? IdempotencyKey = null
) : IRequest<Result<CompileLatexResult>>, IIdempotentRequest;

public sealed record CompileLatexResult(
    byte[] PdfContent,
    string FileName
);
