using CoverLetter.Application.Common.Extensions;
using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Repositories;
using CoverLetter.Application.UseCases.GenerateCoverLetter;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.UseCases.CustomizeCv;

/// <summary>
/// Handler for CustomizeCvCommand.
/// Uses AI to map CV text into a professional LaTeX template tailored to a job description.
/// </summary>
public sealed class CustomizeCvHandler(
    ILlmService llmService,
    ILatexCompilerService latexCompilerService,
    ICvRepository cvRepository,
    IUserContext userContext,
    IPromptRegistry promptRegistry,
    ICustomPromptService customPromptService,
    ILogger<CustomizeCvHandler> logger)
    : IRequestHandler<CustomizeCvCommand, Result<CustomizeCvResult>>
{
    public async Task<Result<CustomizeCvResult>> Handle(
        CustomizeCvCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = logger.BeginHandlerScope(userContext, "CustomizeCv", new()
            {
                ["CvId"] = request.CvId,
                ["HasCustomPrompt"] = !string.IsNullOrWhiteSpace(request.CustomPromptTemplate)
            });

            // 1. Resolve CV text
            var cv = await cvRepository.GetByIdAsync(request.CvId, cancellationToken);
            if (cv is null)
            {
                logger.LogWarning("CV not found: {CvId}", request.CvId);
                return Result<CustomizeCvResult>.Failure($"CV not found: {request.CvId}", ResultType.NotFound);
            }

            // 2. Clean Unicode from job description to prevent LaTeX issues
            var cleanedJobDescription = System.Text.RegularExpressions.Regex.Replace(
                request.JobDescription,
                @"[^\x00-\x7F]", // Remove non-ASCII characters
                "");

            // 3. Build the variables
            var confirmedSkills = "";
            if (request.SelectedKeywords != null && request.SelectedKeywords.Any())
            {
                var keywordsList = string.Join(", ", request.SelectedKeywords);
                confirmedSkills = $"**CONFIRMED SKILLS**: The user has specifically confirmed they possess the following skills: {keywordsList}. You MUST ensure these are clearly integrated into the CV.";
            }

            var variables = new Dictionary<string, string>
            {
                { "JobDescription", cleanedJobDescription },
                { "CvText", cv.Content },
                { "ConfirmedSkills", confirmedSkills }
            };

            // Fetch custom prompt from settings if exists
            var savedCustomPrompt = await customPromptService.GetUserPromptAsync(PromptType.CvCustomization, cancellationToken);

            string Resolve(string template) =>
                variables.Aggregate(template, (c, kv) => c.Replace("{" + kv.Key + "}", kv.Value));

            string finalPrompt;

            // ── Override mode: inline replaces everything for this call only ──────────
            if (!string.IsNullOrWhiteSpace(request.CustomPromptTemplate) && request.PromptMode == PromptMode.Override)
            {
                logger.LogInformation("Using inline prompt in Override mode (full replacement for this call)");
                var resolved = Resolve(request.CustomPromptTemplate);
                // Inject any missing context so the LLM always has what it needs
                if (!request.CustomPromptTemplate.Contains("{JobDescription}"))
                    resolved += $"\n\nJOB DESCRIPTION:\n{variables["JobDescription"]}";
                if (!request.CustomPromptTemplate.Contains("{CvText}"))
                    resolved += $"\n\nCANDIDATE'S CV:\n{variables["CvText"]}";
                if (!string.IsNullOrWhiteSpace(variables["ConfirmedSkills"]) && !request.CustomPromptTemplate.Contains("{ConfirmedSkills}"))
                    resolved += $"\n\n{variables["ConfirmedSkills"]}";
                finalPrompt = resolved;
            }
            else
            {
                // ── Base = saved prompt if exists, otherwise default registry ────────
                string basePrompt;
                if (!string.IsNullOrWhiteSpace(savedCustomPrompt))
                {
                    logger.LogInformation("Using saved prompt from Settings as base");
                    basePrompt = Resolve(savedCustomPrompt);
                }
                else
                {
                    logger.LogInformation("Using default prompt template as base");
                    var promptResult = promptRegistry.GetPrompt(PromptType.CvCustomization, variables);
                    if (promptResult.IsFailure)
                        return Result<CustomizeCvResult>.Failure(promptResult.Errors, promptResult.Type);
                    basePrompt = promptResult.Value!;
                }

                // ── Append mode: add inline instructions on top of the base ─────────
                if (!string.IsNullOrWhiteSpace(request.CustomPromptTemplate))
                {
                    logger.LogInformation("Appending inline instructions to base prompt");
                    finalPrompt = $"{basePrompt}\n\n### ADDITIONAL USER INSTRUCTIONS:\n{Resolve(request.CustomPromptTemplate)}";
                }
                else
                {
                    finalPrompt = basePrompt;
                }
            }

            var userApiKey = userContext.GetUserApiKey();
            var llmOptions = new LlmGenerationOptions(
                SystemMessage: "You are a professional LaTeX CV expert. Respond ONLY with raw LaTeX code, no extra text, no markdown fences.",
                ApiKey: userApiKey
            );

            if (string.IsNullOrWhiteSpace(finalPrompt))
            {
                logger.LogError("Final prompt is empty after processing");
                return Result<CustomizeCvResult>.Failure("Failed to build prompt for CV customization");
            }

            logger.LogInformation("Generating customized LaTeX for CV {CvId}", request.CvId);
            var llmResponse = await llmService.GenerateAsync(finalPrompt, llmOptions, cancellationToken);

            var latexSource = ExtractLatexFromResponse(llmResponse.Content);

            // 3. Compile LaTeX to PDF (unless LaTeX only requested)
            byte[]? pdfBytes = null;
            if (!request.ReturnLatexOnly)
            {
                logger.LogInformation("Compiling customized LaTeX to PDF for CV {CvId}", request.CvId);
                pdfBytes = await latexCompilerService.CompileToPdfAsync(latexSource, cancellationToken);
            }

            var result = new CustomizeCvResult(
                PdfContent: pdfBytes,
                LatexSource: latexSource,
                FileName: request.ReturnLatexOnly ? "customized_cv.tex" : "customized_cv.pdf",
                Model: llmResponse.Model,
                PromptTokens: llmResponse.PromptTokens,
                CompletionTokens: llmResponse.CompletionTokens,
                GeneratedAt: DateTime.UtcNow
            );

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to customize CV for {CvId}", request.CvId);
            return Result.Failure<CustomizeCvResult>($"Failed to customize CV: {ex.Message}");
        }
    }

    private string ExtractLatexFromResponse(string content)
    {
        content = content.Trim();

        // 1. Remove Markdown code fences
        if (content.StartsWith("```"))
        {
            var firstLineEnd = content.IndexOf('\n');
            if (firstLineEnd != -1)
            {
                content = content[(firstLineEnd + 1)..];
            }
            if (content.EndsWith("```"))
            {
                content = content[..^3];
            }
            content = content.Trim();
        }

        // 2. Remove leading/trailing quotes if AI wrapped the whole thing in a string
        if (content.Length >= 2 && content.StartsWith("\"") && content.EndsWith("\""))
        {
            content = content[1..^1];
        }

        // 3. Handle literal \n and \\ if AI still escaped them
        // If it starts with an escaped backslash for documentclass, it's definitely escaped
        if (content.StartsWith("\\\\documentclass") || (content.Contains("\\n") && !content.Contains("\n")))
        {
            content = content
                .Replace("\\n", "\n")
                .Replace("\\\\", "\\")
                .Replace("\\\"", "\"")
                .Replace("\\&", "&") // Common escapes that might be doubled
                .Replace("\\#", "#")
                .Replace("\\$", "$");

            // Note: Since we are unescaping \\ to \, we must be careful with legitimate LaTeX line breaks \\.
            // If the AI sent \\\\, it becomes \\ (correct LaTeX break).
        }

        return content.Trim();
    }
}
