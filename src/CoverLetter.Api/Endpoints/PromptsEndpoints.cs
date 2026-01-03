using Asp.Versioning;
using CoverLetter.Api.Extensions;
using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Domain.Common;
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

        group.MapGet("/preview", PreviewPrompt)
            .WithName("PreviewPrompt")
            .WithSummary("Preview a filled prompt template")
            .WithDescription("Returns a preview of how the prompt will look with your data filled in.")
            .Produces<PromptPreviewResponse>();
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

    private static IResult PreviewPrompt(
        [FromQuery] string type,
        [FromQuery] string? jobDescription,
        [FromQuery] string? cvText,
        [FromServices] IPromptRegistry promptRegistry)
    {
        if (!Enum.TryParse<PromptType>(type, ignoreCase: true, out var promptType))
        {
            return Result<PromptPreviewResponse>.ValidationError(
                "Invalid prompt type. Valid values: CvCustomization, CoverLetter, MatchAnalysis").ToHttpResult();
        }

        var variables = new Dictionary<string, string>
        {
            { "JobDescription", jobDescription ?? "[Job description will appear here]" },
            { "CvText", cvText ?? "[CV content will appear here]" },
            { "ConfirmedSkills", "" }
        };

        var previewResult = promptRegistry.GetPrompt(promptType, variables);
        if (previewResult.IsFailure)
        {
            return previewResult.ToHttpResult();
        }

        var response = new PromptPreviewResponse
        {
            Type = promptType.ToString(),
            Preview = previewResult.Value!,
            EstimatedTokens = previewResult.Value!.Length / 4 // Rough estimate: 1 token ≈ 4 chars
        };

        return Result<PromptPreviewResponse>.Success(response).ToHttpResult();
    }
}

public record PromptTemplatesResponse
{
    public string CvCustomization { get; init; } = string.Empty;
    public string CoverLetter { get; init; } = string.Empty;
    public string MatchAnalysis { get; init; } = string.Empty;
}

public record PromptPreviewResponse
{
    public string Type { get; init; } = string.Empty;
    public string Preview { get; init; } = string.Empty;
    public int EstimatedTokens { get; init; }
}
