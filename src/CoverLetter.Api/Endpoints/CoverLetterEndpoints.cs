using CoverLetter.Api.Extensions;
using CoverLetter.Application.UseCases.GenerateCoverLetter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CoverLetter.Api.Endpoints;

/// <summary>
/// Cover letter generation endpoints.
/// </summary>
public static class CoverLetterEndpoints
{
  public static IEndpointRouteBuilder MapCoverLetterEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes
        .MapGroup("/cover-letters")
        .WithTags("Cover Letter");

    group.MapPost("/generate", GenerateCoverLetter)
        .WithName("GenerateCoverLetter")
        .Produces<GenerateCoverLetterResult>(StatusCodes.Status200OK)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)  // FluentValidation errors
        .ProducesProblem(StatusCodes.Status400BadRequest)                      // Business validation errors
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

    return routes;
  }

  private static async Task<IResult> GenerateCoverLetter(
      GenerateCoverLetterRequest request,
      ISender mediator,
      CancellationToken cancellationToken)
  {
    // Validation exceptions are handled by GlobalExceptionHandler
    var command = new GenerateCoverLetterCommand(
        JobDescription: request.JobDescription,
        CvText: request.CvText,
        CustomPromptTemplate: request.CustomPromptTemplate,
        IdempotencyKey: request.IdempotencyKey
    );

    var result = await mediator.Send(command, cancellationToken);

    return result.ToHttpResult();  // Clean one-liner!
  }
}


/// <summary>
/// Request DTO for generating a cover letter.
/// </summary>
public sealed record GenerateCoverLetterRequest(
    string JobDescription,
    string CvText,
    string? CustomPromptTemplate = null,
    string? IdempotencyKey = null
);