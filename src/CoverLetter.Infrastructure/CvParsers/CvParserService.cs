using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Entities;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

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
        var hyperlinks = new List<Hyperlink>();

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

          // Extract hyperlinks from page annotations
          var pageHyperlinks = ExtractHyperlinksFromPage(page);
          hyperlinks.AddRange(pageHyperlinks);
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
            hyperlinks: hyperlinks,
            metadata: metadata);

        logger.LogInformation(
            "Successfully parsed PDF: {FileName}, Pages: {PageCount}, Characters: {CharCount}, Hyperlinks: {HyperlinkCount}",
            fileName, pageCount, metadata.CharacterCount, hyperlinks.Count);

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

      var hyperlinks = ExtractHyperlinksFromText(text);
      var metadata = CvMetadata.FromText(text, fileSize: fileContent.Length);
      var cvDocument = CvDocument.Create(
          fileName: fileName,
          format: CvFormat.PlainText,
          extractedText: text,
          hyperlinks: hyperlinks,
          metadata: metadata);

      logger.LogInformation(
          "Successfully parsed plain text: {FileName}, Characters: {CharCount}, Hyperlinks: {HyperlinkCount}",
          fileName, metadata.CharacterCount, hyperlinks.Count);

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
      var hyperlinks = ExtractHyperlinksFromLaTeX(latexSource);

      var metadata = CvMetadata.FromText(extractedText, fileSize: fileContent.Length);
      var cvDocument = CvDocument.Create(
          fileName: fileName,
          format: CvFormat.LaTeX,
          extractedText: extractedText,
          originalContent: latexSource, // Preserve source for future customization
          hyperlinks: hyperlinks,
          metadata: metadata);

      logger.LogInformation(
          "Successfully parsed LaTeX: {FileName}, Characters: {CharCount}, Hyperlinks: {HyperlinkCount}",
          fileName, metadata.CharacterCount, hyperlinks.Count);

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

  /// <summary>
  /// Extracts hyperlinks from PDF page annotations and text.
  /// Includes link annotations and text-based URLs (email, http/https).
  /// </summary>
  private List<Hyperlink> ExtractHyperlinksFromPage(UglyToad.PdfPig.Content.Page page)
  {
    var hyperlinks = new List<Hyperlink>();
    var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // Extract from link annotations
    var annotations = page.GetAnnotations();
    if (annotations != null && annotations.Any())
    {
      foreach (var annotation in annotations)
      {
        if (annotation.Type == UglyToad.PdfPig.Annotations.AnnotationType.Link)
        {
          try
          {
            var annotDict = annotation.AnnotationDictionary;

            // Try to get URI from action dictionary
            if (annotDict.TryGet(UglyToad.PdfPig.Tokens.NameToken.A, out var actionToken))
            {
              if (actionToken is UglyToad.PdfPig.Tokens.DictionaryToken actionDict)
              {
                if (actionDict.TryGet(UglyToad.PdfPig.Tokens.NameToken.Create("URI"), out var uriToken))
                {
                  var url = uriToken?.ToString()?.Trim();
                  if (string.IsNullOrWhiteSpace(url)) continue;

                  // Clean up PDF token formatting (remove wrapping parentheses, angle brackets, etc.)
                  url = url.Trim('(', ')', '<', '>', '[', ']', ' ');
                  
                    if (!string.IsNullOrWhiteSpace(url) && seenUrls.Add(url))
                  {
                    hyperlinks.Add(new Hyperlink(
                        Url: url,
                        Type: CategorizeUrl(url)));
                  }
                }
              }
            }
          }
          catch
          {
            // Skip malformed annotations
          }
        }
      }
    }

    // Extract URLs from text content (email, http/https)
    var text = page.Text;
    var textHyperlinks = ExtractHyperlinksFromText(text);
    foreach (var hyperlink in textHyperlinks)
    {
      if (seenUrls.Add(hyperlink.Url))
      {
        hyperlinks.Add(hyperlink);
      }
    }

    return hyperlinks;
  }

  /// <summary>
  /// Categorizes URL type for better context in prompts.
  /// </summary>
  private static HyperlinkType CategorizeUrl(string url)
  {
    if (url.Contains("@") || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
      return HyperlinkType.Email;

    if (url.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase))
      return HyperlinkType.LinkedIn;

    if (url.Contains("github.com", StringComparison.OrdinalIgnoreCase))
      return HyperlinkType.GitHub;

    // Common portfolio domains
    if (url.Contains("portfolio", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("behance.com", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("dribbble.com", StringComparison.OrdinalIgnoreCase))
      return HyperlinkType.Portfolio;

    return HyperlinkType.General;
  }

  /// <summary>
  /// Extracts hyperlinks from plain text using regex patterns.
  /// </summary>
  private static List<Hyperlink> ExtractHyperlinksFromText(string text)
  {
    var hyperlinks = new List<Hyperlink>();
    var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    var urlPatterns = new[]
    {
      @"(?<=^|\s|⋄|\(|\||,|;|:)[A-Za-z0-9][A-Za-z0-9._%+-]*@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", // Email - must start after whitespace or separator
      @"\bhttps?://[^\s<>""']+\b" // HTTP/HTTPS URLs
    };

    foreach (var pattern in urlPatterns)
    {
      var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern);
      foreach (System.Text.RegularExpressions.Match match in matches)
      {
        var url = match.Value;
        
        // Filter out invalid emails (e.g., "Egyptosamaelzekred@gmail.com")
        if (url.Contains('@'))
        {
          var localPart = url.Split('@')[0];
          // Skip if local part is suspiciously long (likely a word concatenated with email)
          if (localPart.Length > 30) continue;
          
          // Skip if local part starts with common non-email words
          var invalidPrefixes = new[] { "egypt", "usa", "canada", "location", "address", "city", "country" };
          if (invalidPrefixes.Any(prefix => localPart.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            continue;
        }
        
        if (seenUrls.Add(url))
        {
          var normalizedUrl = url.Contains('@') && !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
              ? $"mailto:{url}"
              : url;

          hyperlinks.Add(new Hyperlink(
              Url: normalizedUrl,
              DisplayText: url,
              Type: CategorizeUrl(normalizedUrl)));
        }
      }
    }

    return hyperlinks;
  }

  /// <summary>
  /// Extracts hyperlinks from LaTeX source.
  /// Parses \href{url}{text}, \url{url}, and plain URLs.
  /// </summary>
  private static List<Hyperlink> ExtractHyperlinksFromLaTeX(string latexSource)
  {
    var hyperlinks = new List<Hyperlink>();
    var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // Extract \href{url}{text}
    var hrefMatches = System.Text.RegularExpressions.Regex.Matches(
        latexSource,
        @"\\href\{([^}]+)\}\{([^}]+)\}",
        System.Text.RegularExpressions.RegexOptions.Singleline);

    foreach (System.Text.RegularExpressions.Match match in hrefMatches)
    {
      var url = match.Groups[1].Value.Trim();
      var displayText = match.Groups[2].Value.Trim();

      if (!string.IsNullOrWhiteSpace(url) && seenUrls.Add(url))
      {
        hyperlinks.Add(new Hyperlink(
            Url: url,
            DisplayText: displayText,
            Type: CategorizeUrl(url)));
      }
    }

    // Extract \url{url}
    var urlMatches = System.Text.RegularExpressions.Regex.Matches(
        latexSource,
        @"\\url\{([^}]+)\}",
        System.Text.RegularExpressions.RegexOptions.Singleline);

    foreach (System.Text.RegularExpressions.Match match in urlMatches)
    {
      var url = match.Groups[1].Value.Trim();

      if (!string.IsNullOrWhiteSpace(url) && seenUrls.Add(url))
      {
        hyperlinks.Add(new Hyperlink(
            Url: url,
            Type: CategorizeUrl(url)));
      }
    }

    // Extract plain URLs from text
    var textHyperlinks = ExtractHyperlinksFromText(latexSource);
    foreach (var hyperlink in textHyperlinks)
    {
      if (seenUrls.Add(hyperlink.Url))
      {
        hyperlinks.Add(hyperlink);
      }
    }

    return hyperlinks;
  }
}
