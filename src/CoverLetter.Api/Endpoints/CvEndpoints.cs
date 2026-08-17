using CoverLetter.Api.Extensions;
using CoverLetter.Application.UseCases.Compile;
using CoverLetter.Application.UseCases.ParseCv;
using CoverLetter.Application.UseCases.CustomizeCv;
using CoverLetter.Application.UseCases.MatchCv;
using CoverLetter.Application.UseCases.GenerateCoverLetter;
using CoverLetter.Application.UseCases.GetCv;
using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Entities;
using CoverLetter.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    cvGroup.MapPost("/parse-text", ParseCvTextAsync)
        .WithSummary("Store CV from pasted text")
        .WithDescription("Accepts CV text directly and stores it. Returns a CV ID for future use with customize or generate endpoints.")
        .Produces<CvDocument>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .DisableAntiforgery();

    cvGroup.MapPost("/customize", CustomizeCvAsync)
        .WithSummary("Customize a CV based on job description (async)")
        .WithDescription("Uses AI to map CV information into a professional LaTeX template, then enqueues compilation. Returns 202 with a job id to poll for the PDF.")
        .Produces<CustomizeCvResult>(StatusCodes.Status202Accepted)
        .Produces<CustomizeCvResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .DisableAntiforgery();

    cvGroup.MapPost("/compile", CompileLatexAsync)
        .WithSummary("Enqueue raw LaTeX compilation (async)")
        .WithDescription("Takes raw LaTeX source and enqueues a compile job. Returns 202 with a job id to poll for the PDF.")
        .Produces<EnqueueCompileResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .DisableAntiforgery();

    cvGroup.MapGet("/compile/status/{jobId}", GetCompileStatusAsync)
        .WithSummary("Poll the status of a compile job")
        .WithDescription("Returns the current status of a compile job, and a download URL when the PDF is ready.")
        .Produces<GetCompileStatusResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

    cvGroup.MapGet("/compile/result/{jobId}", DownloadCompileResultAsync)
        .WithSummary("Download the compiled PDF")
        .WithDescription("Streams the compiled PDF. Returns 404 if the job is unknown or not yet completed, 409 if not completed.")
        .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

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
  /// POST /api/v1/cv/parse-text
  /// Stores CV from pasted text content.
  /// </summary>
  private static async Task<IResult> ParseCvTextAsync(
      [FromBody] ParseCvTextRequest request,
      ISender mediator,
      CancellationToken cancellationToken,
      [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey)
  {
    var fileContent = System.Text.Encoding.UTF8.GetBytes(request.CvText);

    var command = new ParseCvCommand(
        FileName: "cv.txt",
        FileContent: fileContent,
        Format: CvFormat.PlainText,
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

    // 202 when a compile job was enqueued; 200 when LaTeX-only was requested.
    return result.ToHttpResult(result.Value?.JobId is null ? 200 : 202);
  }

  private static async Task<IResult> CompileLatexAsync(
      [FromBody] CompileLatexRequest request,
      ISender mediator,
      CancellationToken cancellationToken,
      [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey)
  {
    var command = new EnqueueCompileCommand(request.LatexSource, idempotencyKey);
    var result = await mediator.Send(command, cancellationToken);
    return result.ToHttpResult(202);
  }

  private static async Task<IResult> GetCompileStatusAsync(
      Guid jobId,
      ISender mediator,
      CancellationToken cancellationToken)
  {
    var query = new GetCompileStatusQuery(jobId);
    var result = await mediator.Send(query, cancellationToken);
    return result.ToHttpResult();
  }

  private static async Task<IResult> DownloadCompileResultAsync(
      Guid jobId,
      ICompileResultStorage storage,
      IQueryContext queryContext,
      CancellationToken cancellationToken)
  {
    var job = await queryContext.CompileJobs
        .Where(x => x.Id == jobId)
        .Select(x => new { x.Status, x.ResultPath })
        .FirstOrDefaultAsync(cancellationToken);

    if (job is null)
      return Results.NotFound(new { error = $"Compile job not found: {jobId}" });

    if (job.Status != CompileJobStatus.Completed)
      return Results.Conflict(new { error = $"Job is not completed (status: {job.Status}).", statusUrl = $"/api/v1/cv/compile/status/{jobId}" });

    var pdf = await storage.ReadAsync(jobId, cancellationToken);
    if (pdf is null)
      return Results.NotFound(new { error = "Compiled PDF is no longer available." });

    return Results.File(pdf, "application/pdf", $"{jobId}.pdf");
  }
}

public sealed record CompileLatexRequest(string LatexSource);

public sealed record ParseCvTextRequest(string CvText);

public sealed record CustomizeCvRequest(
    Guid CvId,
    string JobDescription,
    string? CustomPromptTemplate = null,
    PromptMode PromptMode = PromptMode.Append,
    IEnumerable<string>? SelectedKeywords = null
);
