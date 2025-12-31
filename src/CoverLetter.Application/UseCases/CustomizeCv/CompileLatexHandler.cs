using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.UseCases.CustomizeCv;

public sealed class CompileLatexHandler(
    ILatexCompilerService latexCompilerService,
    ILogger<CompileLatexHandler> logger)
    : IRequestHandler<CompileLatexCommand, Result<CompileLatexResult>>
{
    public async Task<Result<CompileLatexResult>> Handle(CompileLatexCommand request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Compiling user-edited LaTeX to PDF");
            var pdfBytes = await latexCompilerService.CompileToPdfAsync(request.LatexSource, cancellationToken);
            
            var result = new CompileLatexResult(
                PdfContent: pdfBytes,
                FileName: "compiled_cv.pdf"
            );

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to compile LaTeX source");
            return Result.Failure<CompileLatexResult>($"Compilation failed: {ex.Message}");
        }
    }
}
