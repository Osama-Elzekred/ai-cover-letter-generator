using Asp.Versioning;
using CoverLetter.Application.UseCases.GenerateCoverLetter;
using FluentValidation;
using MediatR;
using Refit;

namespace CoverLetter.Api.Endpoints;

/// <summary>
/// Cover letter generation endpoints.
/// </summary>
public static class CoverLetterEndpoints
{
  public static IEndpointRouteBuilder MapCoverLetterEndpoints(this IEndpointRouteBuilder app)
  {
    var apiVersionSet = app.NewApiVersionSet()
        .HasApiVersion(new ApiVersion(1, 0))
        .ReportApiVersions()
        .Build();

    var group = app.MapGroup("/api/v{version:apiVersion}/cover-letters")
        .WithApiVersionSet(apiVersionSet)
        .WithTags("Cover Letter");

    group.MapPost("/generate", GenerateCoverLetter)
        .WithName("GenerateCoverLetter")
        .Produces<GenerateCoverLetterResult>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

    return app;
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
        CustomPromptTemplate: request.CustomPromptTemplate
    );

    var result = await mediator.Send(command, cancellationToken);

    if (result.IsFailure)
    {
      return Results.Problem(
          title: "Generation Failed",
          detail: result.Error,
          statusCode: StatusCodes.Status500InternalServerError
      );
    }

    return Results.Ok(result.Value);
  }
}

/// <summary>
/// Request DTO for generating a cover letter.
/// </summary>
public sealed record GenerateCoverLetterRequest(
    string JobDescription,
    string CvText,
    string? CustomPromptTemplate = null
);
