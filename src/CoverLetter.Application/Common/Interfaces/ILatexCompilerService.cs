namespace CoverLetter.Application.Common.Interfaces;

/// <summary>
/// Service for compiling LaTeX source code to PDF.
/// </summary>
public interface ILatexCompilerService
{
    /// <summary>
    /// Compiles LaTeX source code to PDF.
    /// </summary>
    /// <param name="latexSource">The LaTeX source code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The compiled PDF as a byte array.</returns>
    Task<byte[]> CompileToPdfAsync(string latexSource, CancellationToken cancellationToken);
}
