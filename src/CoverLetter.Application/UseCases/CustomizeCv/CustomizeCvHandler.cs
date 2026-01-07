using CoverLetter.Application.Common.Extensions;
using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.UseCases.GenerateCoverLetter;
using CoverLetter.Domain.Common;
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
            var cvResult = await cvRepository.GetByIdAsync(request.CvId, cancellationToken);
            if (cvResult.IsFailure)
            {
                logger.LogWarning("CV not found: {CvId}", request.CvId);
                return Result<CustomizeCvResult>.Failure(cvResult.Errors, cvResult.Type);
            }
            var cvDocument = cvResult.Value!;

            // 2. Build the variables
            var confirmedSkills = "";
            if (request.SelectedKeywords != null && request.SelectedKeywords.Any())
            {
                var keywordsList = string.Join(", ", request.SelectedKeywords);
                confirmedSkills = $"**CONFIRMED SKILLS**: The user has specifically confirmed they possess the following skills: {keywordsList}. You MUST ensure these are clearly integrated into the CV.";
            }

            var variables = new Dictionary<string, string>
            {
                { "JobDescription", request.JobDescription },
                { "CvText", cvDocument.ExtractedText },
                { "ConfirmedSkills", confirmedSkills }
            };

            // Fetch custom prompt from settings if exists
            var savedCustomPrompt = await customPromptService.GetUserPromptAsync(PromptType.CvCustomization, cancellationToken);

            string finalPrompt;

            // Check if user provided inline prompt (toggle was enabled)
            var hasInlinePrompt = !string.IsNullOrWhiteSpace(request.CustomPromptTemplate);

            if (hasInlinePrompt)
            {
                // Toggle was ON - user provided inline prompt for this request
                // Priority: inline > default
                if (request.PromptMode == PromptMode.Override)
                {
                    // Override mode: Use inline prompt entirely, ignore saved
                    logger.LogInformation("Using inline prompt in Override mode (full replacement)");

                    var overrideVariables = new Dictionary<string, string>
                    {
                        { "JobDescription", variables["JobDescription"] ?? "" },
                        { "CvText", variables["CvText"] ?? "" },
                        { "ConfirmedSkills", variables["ConfirmedSkills"] ?? "" }
                    };

                    finalPrompt = overrideVariables.Aggregate(request.CustomPromptTemplate!, (current, variable) =>
                        current.Replace("{" + variable.Key + "}", variable.Value));
                }
                else
                {
                    // Append mode: Use saved prompt as base, append inline as additional instructions
                    logger.LogInformation("Using saved/default prompt with inline instructions appended");

                    if (!string.IsNullOrWhiteSpace(savedCustomPrompt))
                    {
                        // Use saved prompt as base, append inline
                        var basePrompt = savedCustomPrompt;
                        finalPrompt = variables.Aggregate(basePrompt, (current, variable) =>
                            current.Replace("{" + variable.Key + "}", variable.Value)) +
                            $"\n\n### ADDITIONAL USER INSTRUCTIONS:\n{request.CustomPromptTemplate}";
                    }
                    else
                    {
                        // No saved prompt, use default + append inline
                        var promptResult = promptRegistry.GetPrompt(PromptType.CvCustomization, variables);
                        if (promptResult.IsFailure)
                        {
                            return Result<CustomizeCvResult>.Failure(promptResult.Errors, promptResult.Type);
                        }
                        finalPrompt = promptResult.Value! + $"\n\n### ADDITIONAL USER INSTRUCTIONS:\n{request.CustomPromptTemplate}";
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(savedCustomPrompt))
            {
                // Toggle was OFF - no inline prompt, but saved prompt exists
                // Use saved prompt as the base structure (like Override mode)
                logger.LogInformation("Using saved prompt from Settings (toggle disabled)");

                var overrideVariables = new Dictionary<string, string>
                {
                    { "JobDescription", variables["JobDescription"] },
                    { "CvText", variables["CvText"] },
                    { "ConfirmedSkills", variables["ConfirmedSkills"] }
                };

                finalPrompt = overrideVariables.Aggregate(savedCustomPrompt, (current, variable) =>
                    current.Replace("{" + variable.Key + "}", variable.Value));
            }
            else
            {
                // No custom prompt at all (toggle off, no saved) - use default
                logger.LogInformation("Using default prompt template");
                var promptResult = promptRegistry.GetPrompt(PromptType.CvCustomization, variables);
                if (promptResult.IsFailure)
                {
                    return Result<CustomizeCvResult>.Failure(promptResult.Errors, promptResult.Type);
                }
                finalPrompt = promptResult.Value!;
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
