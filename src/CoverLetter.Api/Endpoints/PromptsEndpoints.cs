using Asp.Versioning;
using CoverLetter.Api.Extensions;
using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CoverLetter.Api.Endpoints;

public static class PromptsEndpoints
{
    public static void MapPromptsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/prompts")
            .WithTags("Prompts");

        group.MapGet("/templates", GetPromptTemplates)
            .WithName("GetPromptTemplates")
            .WithSummary("Get all prompt templates")
            .WithDescription("Returns the raw prompt templates used for CV customization, cover letter generation, and match analysis. Useful for transparency and debugging.")
            .Produces<PromptTemplatesResponse>();
    }

    private static IResult GetPromptTemplates(
        [FromServices] IPromptRegistry promptRegistry)
    {
        var cvCustomizationResult = promptRegistry.GetRawTemplate(PromptType.CvCustomization);
        var coverLetterResult = promptRegistry.GetRawTemplate(PromptType.CoverLetter);
        var matchAnalysisResult = promptRegistry.GetRawTemplate(PromptType.MatchAnalysis);

        // Return failure if any template retrieval failed
        if (cvCustomizationResult.IsFailure)
            return cvCustomizationResult.ToHttpResult();
        if (coverLetterResult.IsFailure)
            return coverLetterResult.ToHttpResult();
        if (matchAnalysisResult.IsFailure)
            return matchAnalysisResult.ToHttpResult();

        var templates = new PromptTemplatesResponse
        {
            CvCustomization = cvCustomizationResult.Value!,
            CoverLetter = coverLetterResult.Value!,
            MatchAnalysis = matchAnalysisResult.Value!
        };

        return Result<PromptTemplatesResponse>.Success(templates).ToHttpResult();
    }
}

public record PromptTemplatesResponse
{
    public string CvCustomization { get; init; } = string.Empty;
    public string CoverLetter { get; init; } = string.Empty;
    public string MatchAnalysis { get; init; } = string.Empty;
}
