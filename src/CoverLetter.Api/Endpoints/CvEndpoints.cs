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
public static partial class CvEndpoints
{
  public static IEndpointRouteBuilder MapCvEndpoints(this IEndpointRouteBuilder routes)
  {
    var cvGroup = routes
        .MapGroup("/cv")
        .WithTags("CV Management");

    cvGroup.MapPost("/parse", ParseCvAsync)
        .WithSummary("Upload and parse a CV file")
        .WithDescription("Accepts PDF, LaTeX, or plain text CV files. Extracts text content and returns a CV ID for future use.")
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<CvDocument>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .DisableAntiforgery();

    cvGroup.MapGet("/{cvId}", GetCvAsync)
        .WithSummary("Retrieve a parsed CV by ID")
        .WithDescription("Returns the parsed CV document from cache. CV must have been parsed within the last 24 hours.")
        .Produces<CvDocument>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

    return routes;
  }

  /// <summary>
  /// POST /api/v1/cv/parse
  /// Uploads and parses a CV file (PDF, LaTeX, or plain text).
  /// </summary>
  private static async Task<IResult> ParseCvAsync(
      [FromForm] ParseCvForm form,
      ISender mediator,
      CancellationToken cancellationToken)
  {
    // Determine format from parameter or file extension
    var cvFormat = DetermineFormat(form.Format, form.File.FileName);
    if (cvFormat is null)
    {
      return Results.BadRequest(new
      {
        error = "Unable to determine CV format. Please specify 'format' parameter (pdf, latex, or plaintext) or use a recognized file extension."
      });
    }

    // Read file content
    using var memoryStream = new MemoryStream();
    await form.File.CopyToAsync(memoryStream, cancellationToken);
    var fileContent = memoryStream.ToArray();

    // Create command and send to handler
    var command = new ParseCvCommand(
        FileName: form.File.FileName,
        FileContent: fileContent,
        Format: cvFormat.Value,
        IdempotencyKey: form.IdempotencyKey);

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
