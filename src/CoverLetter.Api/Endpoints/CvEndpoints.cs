using CoverLetter.Api.Extensions;
using CoverLetter.Application.UseCases.ParseCv;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace CoverLetter.Api.Endpoints;

/// <summary>
/// Endpoints for CV upload and retrieval.
/// </summary>
public static class CvEndpoints
{
  public static IEndpointRouteBuilder MapCvEndpoints(this IEndpointRouteBuilder routes)
  {
    var cvGroup = routes
        .MapGroup("/cv")
        .WithTags("CV Management");

    cvGroup.MapPost("/parse", ParseCvAsync)
        .WithName("ParseCv")
        .WithSummary("Upload and parse a CV file")
        .WithDescription("Accepts PDF, LaTeX, or plain text CV files. Extracts text content and returns a CV ID for future use.")
        .DisableAntiforgery(); // Required for file uploads

    cvGroup.MapGet("/{cvId}", GetCvAsync)
        .WithName("GetCv")
        .WithSummary("Retrieve a parsed CV by ID")
        .WithDescription("Returns the parsed CV document from cache. CV must have been parsed within the last 24 hours.");

    return routes;
  }

  /// <summary>
  /// POST /api/v1/cv/parse
  /// Uploads and parses a CV file (PDF, LaTeX, or plain text).
  /// </summary>
  private static async Task<IResult> ParseCvAsync(
      [FromForm] IFormFile file,
      [FromForm] string? format, // Optional: "pdf", "latex", "plaintext"
      [FromForm] string? idempotencyKey, // Optional: client-generated key for deduplication
      ISender mediator,
      CancellationToken cancellationToken)
  {
    // Determine format from parameter or file extension
    var cvFormat = DetermineFormat(format, file.FileName);
    if (cvFormat is null)
    {
      return Results.BadRequest(new
      {
        error = "Unable to determine CV format. Please specify 'format' parameter (pdf, latex, or plaintext) or use a recognized file extension."
      });
    }

    // Read file content
    using var memoryStream = new MemoryStream();
    await file.CopyToAsync(memoryStream, cancellationToken);
    var fileContent = memoryStream.ToArray();

    // Create command and send to handler
    var command = new ParseCvCommand(
        FileName: file.FileName,
        FileContent: fileContent,
        Format: cvFormat.Value,
        IdempotencyKey: idempotencyKey);

    var result = await mediator.Send(command, cancellationToken);

    return result.ToHttpResult();
  }

  /// <summary>
  /// GET /api/v1/cv/{cvId}
  /// Retrieves a parsed CV document from cache.
  /// </summary>
  private static IResult GetCvAsync(
      string cvId,
      IMemoryCache cache)
  {
    var cacheKey = $"cv:{cvId}";

    if (!cache.TryGetValue<CvDocument>(cacheKey, out var cvDocument) || cvDocument is null)
    {
      var notFoundResult = Result<ParseCvResult>.NotFound(
          "CV not found. The CV may have expired (24h cache) or the ID is invalid.");
      return notFoundResult.ToHttpResult();
    }

    var parseCvResult = ParseCvResult.FromDocument(cvDocument);
    var result = Result<ParseCvResult>.Success(parseCvResult);
    return result.ToHttpResult();
  }

  /// <summary>
  /// Determines CV format from parameter or file extension.
  /// </summary>
  private static CvFormat? DetermineFormat(string? formatParam, string fileName)
  {
    // Try parameter first
    if (!string.IsNullOrWhiteSpace(formatParam))
    {
      return formatParam.ToLowerInvariant() switch
      {
        "pdf" => CvFormat.Pdf,
        "latex" or "tex" => CvFormat.LaTeX,
        "plaintext" or "text" or "txt" => CvFormat.PlainText,
        _ => null
      };
    }

    // Fallback to file extension
    var extension = Path.GetExtension(fileName).ToLowerInvariant();
    return extension switch
    {
      ".pdf" => CvFormat.Pdf,
      ".tex" or ".latex" => CvFormat.LaTeX,
      ".txt" or ".text" => CvFormat.PlainText,
      _ => null
    };
  }
}
