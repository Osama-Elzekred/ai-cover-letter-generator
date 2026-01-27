using CoverLetter.Api.Extensions;
using CoverLetter.Application.UseCases.AnswerTextareaQuestion;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CoverLetter.Api.Endpoints;

/// <summary>
/// Textarea question answering endpoints for job applications.
/// Generates AI-powered answers to job application textarea fields using stored CV information.
/// </summary>
public static class TextareaAnswerEndpoints
{
  public static IEndpointRouteBuilder MapTextareaAnswerEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes
        .MapGroup("/textarea-answers")
        .WithTags("Textarea Answers");

    group.MapPost("/generate", AnswerQuestion)
        .WithSummary("Generate an answer to a textarea question using CV")
        .WithDescription("Generates a focused, professional answer to job application textarea questions (e.g., 'Why are you interested?', 'Tell us about yourself') using the candidate's stored CV and optional job context. Rate limited to 10 requests/minute for users without saved API keys.")
        .RequireRateLimiting("ByokPolicy")  // Apply BYOK-aware rate limiting
        .Produces<AnswerQuestionResult>(StatusCodes.Status200OK)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

    return routes;
  }

  private static async Task<IResult> AnswerQuestion(
      AnswerQuestionRequest request,
      ISender mediator,
      CancellationToken cancellationToken,
      [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey)
  {
    var command = new AnswerQuestionCommand(
        CvId: request.CvId,
        FieldLabel: request.FieldLabel,
        UserQuestion: request.UserQuestion,
        JobTitle: request.JobTitle,
        CompanyName: request.CompanyName,
        JobDescription: request.JobDescription,
        CustomPromptTemplate: request.CustomPromptTemplate,
        IdempotencyKey: idempotencyKey
    );

    var result = await mediator.Send(command, cancellationToken);

    return result.ToHttpResult();
  }
}

/// <summary>
/// Request DTO for answering a textarea question using CV information.
/// </summary>
public sealed record AnswerQuestionRequest(
    string CvId,
    string FieldLabel,
    string UserQuestion,
    string? JobTitle = null,
    string? CompanyName = null,
    string? JobDescription = null,
    string? CustomPromptTemplate = null
);
