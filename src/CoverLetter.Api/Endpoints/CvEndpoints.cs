using CoverLetter.Api.Extensions;
using CoverLetter.Application.UseCases.ParseCv;
using CoverLetter.Application.UseCases.CustomizeCv;
using CoverLetter.Application.UseCases.MatchCv;
using CoverLetter.Application.UseCases.GenerateCoverLetter;
using CoverLetter.Application.UseCases.GetCv;
using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

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

    cvGroup.MapPost("/customize", CustomizeCvAsync)
        .WithSummary("Customize a CV based on job description")
        .WithDescription("Uses AI to map CV information into a professional LaTeX template. Returns both PDF and LaTeX source.")
        .Produces<CustomizeCvResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .DisableAntiforgery();

    cvGroup.MapPost("/compile", CompileLatexAsync)
        .WithSummary("Compile raw LaTeX to PDF")
        .WithDescription("Takes raw LaTeX source and returns a compiled PDF file.")
        .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .DisableAntiforgery();

    cvGroup.MapGet("/{cvId}", GetCvAsync)
        .WithSummary("Retrieve a parsed CV by ID")
        .WithDescription("Returns the parsed CV document. CV must have been previously parsed and saved.")
        .Produces<GetCvResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

    cvGroup.MapMethods("/{cvId}", ["HEAD"], CvExistsAsync)
    .WithSummary("Check if a parsed CV exists by ID (HEAD)")
    .WithDescription("Returns 200 if the CV exists, 404 if not. No body is returned.")
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

    cvGroup.MapPost("/match", MatchCvAsync)
        .WithSummary("Analyze CV match with job description")
        .WithDescription("Uses AI to calculate a match score and identify matching/missing keywords.")
        .Produces<MatchCvResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .DisableAntiforgery();

    return routes;
  }

  private static async Task<IResult> MatchCvAsync(
      [FromBody] MatchCvRequest request,
      ISender mediator,
      CancellationToken cancellationToken,
      [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey)
  {
    var command = new MatchCvCommand(request.CvId, request.JobDescription, IdempotencyKey: idempotencyKey);

    var result = await mediator.Send(command, cancellationToken);
    return result.ToHttpResult();
  }

  public sealed record MatchCvRequest(Guid CvId, string JobDescription);

  private static async Task<IResult> CvExistsAsync(
      Guid cvId,
      ICvRepository cvRepository,
      CancellationToken cancellationToken)
  {
    var exists = await cvRepository.ExistsAsync(cvId, cancellationToken);
    return exists ? Results.Ok() : Results.NotFound();
  }

  /// <summary>
  /// POST /api/v1/cv/parse
  /// Uploads and parses a CV file (PDF, LaTeX, or plain text).
  /// </summary>
  private static async Task<IResult> ParseCvAsync(
      [FromForm] ParseCvForm form,
      ISender mediator,
      CancellationToken cancellationToken,
      [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey)
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


    // Extract idempotency key is now handled by parameter binding
    // var idempotencyKey = httpContext.GetIdempotencyKey();

    // Create command and send to handler
    var command = new ParseCvCommand(
        FileName: form.File.FileName,
        FileContent: fileContent,
        Format: cvFormat.Value,
        IdempotencyKey: idempotencyKey);

    var result = await mediator.Send(command, cancellationToken);

    return result.ToHttpResult();
  }

  /// <summary>
  /// GET /api/v1/cv/{cvId}
  /// Retrieves a parsed CV document from the database.
  /// </summary>
  private static async Task<IResult> GetCvAsync(
      Guid cvId,
      ISender mediator,
      CancellationToken cancellationToken)
  {
    var query = new GetCvQuery(cvId);
    var result = await mediator.Send(query, cancellationToken);
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

  /// <summary>
  /// POST /api/v1/cv/customize
  /// Generates a customized CV in PDF format.
  /// </summary>
  private static async Task<IResult> CustomizeCvAsync(
      [FromBody] CustomizeCvRequest request,
      ISender mediator,
      CancellationToken cancellationToken,
      [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey)
  {
    var command = new CustomizeCvCommand(
        request.CvId,
        request.JobDescription,
        CustomPromptTemplate: request.CustomPromptTemplate,
        PromptMode: request.PromptMode,
        SelectedKeywords: request.SelectedKeywords,
        IdempotencyKey: idempotencyKey);

    var result = await mediator.Send(command, cancellationToken);
    return result.ToHttpResult();
  }

  private static async Task<IResult> CompileLatexAsync(
      [FromBody] CompileLatexRequest request,
      ISender mediator,
      CancellationToken cancellationToken,
      [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey)
  {
    var command = new CompileLatexCommand(request.LatexSource, idempotencyKey);
    var result = await mediator.Send(command, cancellationToken);

    if (result.IsFailure) return result.ToHttpResult();

    return Results.File(result.Value.PdfContent, "application/pdf", result.Value.FileName);
  }
}

public sealed record CompileLatexRequest(string LatexSource);

public sealed record CustomizeCvRequest(
    Guid CvId,
    string JobDescription,
    string? CustomPromptTemplate = null,
    PromptMode PromptMode = PromptMode.Append,
    IEnumerable<string>? SelectedKeywords = null
);
