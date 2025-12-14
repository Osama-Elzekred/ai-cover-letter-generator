using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Entities;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace CoverLetter.Infrastructure.CvParsers;

/// <summary>
/// PDF CV parser using PdfPig library.
/// Extracts text content and metadata from PDF files.
/// </summary>
public sealed class CvParserService(ILogger<CvParserService> logger) : ICvParserService
{
  public async Task<Result<CvDocument>> ParseAsync(
      string fileName,
      byte[] fileContent,
      CvFormat format,
      CancellationToken cancellationToken = default)
  {
    logger.LogDebug("Parsing CV file: {FileName}, Format: {Format}, Size: {Size} bytes",
        fileName, format, fileContent.Length);

    return format switch
    {
      CvFormat.Pdf => await ParsePdfAsync(fileName, fileContent, cancellationToken),
      CvFormat.PlainText => ParsePlainText(fileName, fileContent),
      CvFormat.LaTeX => ParseLaTeX(fileName, fileContent),
      _ => Result<CvDocument>.Failure($"Unsupported CV format: {format}", ResultType.NotSupported)
    };
  }

  private async Task<Result<CvDocument>> ParsePdfAsync(
      string fileName,
      byte[] fileContent,
      CancellationToken cancellationToken)
  {
    try
    {
      // PdfPig operations are synchronous, wrap in Task.Run for cancellation support
      return await Task.Run(() =>
      {
        cancellationToken.ThrowIfCancellationRequested();

        using var document = PdfDocument.Open(fileContent);
        var pageCount = document.NumberOfPages;
        var textBuilder = new System.Text.StringBuilder();

        foreach (var page in document.GetPages())
        {
          cancellationToken.ThrowIfCancellationRequested();

          // Use GetWords() instead of .Text for better word boundary detection
          var words = page.GetWords();
          foreach (var word in words)
          {
            textBuilder.Append(word.Text);
            textBuilder.Append(' '); // Add space between words
          }

          textBuilder.AppendLine(); // Add newline between pages
          textBuilder.AppendLine();
        }

        var extractedText = textBuilder.ToString().Trim();

        if (string.IsNullOrWhiteSpace(extractedText))
        {
          logger.LogWarning("PDF extraction resulted in empty text: {FileName}", fileName);
          return Result<CvDocument>.Failure(
              "PDF file appears to be empty or contains only images. Text extraction failed.",
              ResultType.InvalidInput);
        }

        var metadata = CvMetadata.FromText(
            extractedText,
            fileSize: fileContent.Length,
            pageCount: pageCount);

        var cvDocument = CvDocument.Create(
            fileName: fileName,
            format: CvFormat.Pdf,
            extractedText: extractedText,
            metadata: metadata);

        logger.LogInformation(
            "Successfully parsed PDF: {FileName}, Pages: {PageCount}, Characters: {CharCount}",
            fileName, pageCount, metadata.CharacterCount);

        return Result<CvDocument>.Success(cvDocument);
      }, cancellationToken);
    }
    catch (OperationCanceledException)
    {
      logger.LogDebug("PDF parsing cancelled: {FileName}", fileName);
      throw;
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed to parse PDF: {FileName}", fileName);
      return Result<CvDocument>.Failure(
          $"Failed to parse PDF file. The file may be corrupted or password-protected. Error: {ex.Message}",
          ResultType.InvalidInput);
    }
  }

  private Result<CvDocument> ParsePlainText(string fileName, byte[] fileContent)
  {
    try
    {
      var text = System.Text.Encoding.UTF8.GetString(fileContent).Trim();

      if (string.IsNullOrWhiteSpace(text))
      {
        return Result<CvDocument>.Failure(
            "Text file is empty.",
            ResultType.InvalidInput);
      }

      var metadata = CvMetadata.FromText(text, fileSize: fileContent.Length);
      var cvDocument = CvDocument.Create(
          fileName: fileName,
          format: CvFormat.PlainText,
          extractedText: text,
          metadata: metadata);

      logger.LogInformation(
          "Successfully parsed plain text: {FileName}, Characters: {CharCount}",
          fileName, metadata.CharacterCount);

      return Result<CvDocument>.Success(cvDocument);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed to parse plain text: {FileName}", fileName);
      return Result<CvDocument>.Failure(
          $"Failed to parse text file. Error: {ex.Message}",
          ResultType.InvalidInput);
    }
  }

  private Result<CvDocument> ParseLaTeX(string fileName, byte[] fileContent)
  {
    try
    {
      var latexSource = System.Text.Encoding.UTF8.GetString(fileContent).Trim();

      if (string.IsNullOrWhiteSpace(latexSource))
      {
        return Result<CvDocument>.Failure(
            "LaTeX file is empty.",
            ResultType.InvalidInput);
      }

      // For LaTeX, we store both the original source and a simplified text version
      // Future: Implement proper LaTeX-to-text conversion (remove commands, keep content)
      var extractedText = SimplifyLaTeX(latexSource);

      var metadata = CvMetadata.FromText(extractedText, fileSize: fileContent.Length);
      var cvDocument = CvDocument.Create(
          fileName: fileName,
          format: CvFormat.LaTeX,
          extractedText: extractedText,
          originalContent: latexSource, // Preserve source for future customization
          metadata: metadata);

      logger.LogInformation(
          "Successfully parsed LaTeX: {FileName}, Characters: {CharCount}",
          fileName, metadata.CharacterCount);

      return Result<CvDocument>.Success(cvDocument);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed to parse LaTeX: {FileName}", fileName);
      return Result<CvDocument>.Failure(
          $"Failed to parse LaTeX file. Error: {ex.Message}",
          ResultType.InvalidInput);
    }
  }

  /// <summary>
  /// Simplified LaTeX-to-text conversion.
  /// Removes common LaTeX commands while preserving content.
  /// TODO: Enhance for production (handle more commands, environments, etc.)
  /// </summary>
  private static string SimplifyLaTeX(string latexSource)
  {
    // Basic cleanup: remove common LaTeX commands
    var text = latexSource;

    // Remove document preamble commands
    text = System.Text.RegularExpressions.Regex.Replace(
        text,
        @"\\documentclass\{.*?\}|\\usepackage(\[.*?\])?\{.*?\}|\\begin\{document\}|\\end\{document\}",
        "",
        System.Text.RegularExpressions.RegexOptions.Singleline);

    // Remove section commands but keep the content
    text = System.Text.RegularExpressions.Regex.Replace(
        text,
        @"\\(section|subsection|subsubsection|title|author|date)\{(.*?)\}",
        "$2",
        System.Text.RegularExpressions.RegexOptions.Singleline);

    // Remove formatting commands
    text = System.Text.RegularExpressions.Regex.Replace(
        text,
        @"\\(textbf|textit|emph|underline)\{(.*?)\}",
        "$2",
        System.Text.RegularExpressions.RegexOptions.Singleline);

    // Remove remaining commands
    text = System.Text.RegularExpressions.Regex.Replace(
        text,
        @"\\[a-zA-Z]+(\[.*?\])?(\{.*?\})?",
        "",
        System.Text.RegularExpressions.RegexOptions.Singleline);

    // Clean up whitespace
    text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

    return text.Trim();
  }
}
