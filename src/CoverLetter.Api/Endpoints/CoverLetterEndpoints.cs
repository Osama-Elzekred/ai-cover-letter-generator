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
            .WithTags("Cover Letters");

        // Primary endpoint: Generate from uploaded CV (by ID)
        group.MapPost("/generate", GenerateCoverLetterFromCvId)
            .WithSummary("Generate a cover letter from uploaded CV")
            .WithDescription("Generates a personalized cover letter using a previously uploaded CV (referenced by ID). Upload CV via POST /cv/parse first. Rate limited to 10 requests/minute for users without saved API keys.")
            .RequireRateLimiting("ByokPolicy")  // Apply BYOK-aware rate limiting
            .Produces<GenerateCoverLetterResult>(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // Convenience endpoint: Generate from direct text input
        group.MapPost("/generate-from-text", GenerateCoverLetterFromText)
            .WithSummary("Generate a cover letter from direct CV text")
            .WithDescription("Generates a personalized cover letter using CV text provided directly in the request. For one-time use; prefer uploading CV and using /generate for better performance. Rate limited to 10 requests/minute for users without saved API keys.")
            .RequireRateLimiting("ByokPolicy")  // Apply BYOK-aware rate limiting
            .Produces<GenerateCoverLetterResult>(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return routes;
    }

    private static async Task<IResult> GenerateCoverLetterFromCvId(
        GenerateCoverLetterFromCvIdRequest request,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var command = new GenerateCoverLetterCommand(
            JobDescription: request.JobDescription,
            CvId: request.CvId,
            CvText: null,
            CustomPromptTemplate: request.CustomPromptTemplate,
            PromptMode: request.PromptMode,
            IdempotencyKey: request.IdempotencyKey
        );

        var result = await mediator.Send(command, cancellationToken);

        return result.ToHttpResult();
    }

    private static async Task<IResult> GenerateCoverLetterFromText(
        GenerateCoverLetterFromTextRequest request,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var command = new GenerateCoverLetterCommand(
            JobDescription: request.JobDescription,
            CvId: null,
            CvText: request.CvText,
            CustomPromptTemplate: request.CustomPromptTemplate,
            PromptMode: request.PromptMode,
            IdempotencyKey: request.IdempotencyKey
        );

        var result = await mediator.Send(command, cancellationToken);

        return result.ToHttpResult();
    }
}


/// <summary>
/// Request DTO for generating a cover letter from uploaded CV (by ID).
/// </summary>
public sealed record GenerateCoverLetterFromCvIdRequest(
    string JobDescription,
    string CvId,
    string? CustomPromptTemplate = null,
    PromptMode PromptMode = PromptMode.Append,
    string? IdempotencyKey = null
);

/// <summary>
/// Request DTO for generating a cover letter from direct CV text input.
/// </summary>
public sealed record GenerateCoverLetterFromTextRequest(
    string JobDescription,
    string CvText,
    string? CustomPromptTemplate = null,
    PromptMode PromptMode = PromptMode.Append,
    string? IdempotencyKey = null
);