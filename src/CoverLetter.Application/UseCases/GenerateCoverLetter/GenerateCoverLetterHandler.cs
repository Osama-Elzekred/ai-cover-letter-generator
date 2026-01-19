using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.UseCases.GenerateCoverLetter;

/// <summary>
/// Handler for GenerateCoverLetterCommand.
/// This is where the business logic lives.
/// Uses IUserContext to access current user information and BYOK API keys.
/// </summary>
public sealed class GenerateCoverLetterHandler(
    ILlmService llmService,
    ICvRepository cvRepository,
    IUserContext userContext,
    IPromptRegistry promptRegistry,
    ICustomPromptService customPromptService,
    ILogger<GenerateCoverLetterHandler> logger)
    : IRequestHandler<GenerateCoverLetterCommand, Result<GenerateCoverLetterResult>>
{
  public async Task<Result<GenerateCoverLetterResult>> Handle(
      GenerateCoverLetterCommand request,
      CancellationToken cancellationToken)
  {
    try
    {
      using var scope = logger.BeginScope(new Dictionary<string, object>
      {
        ["UserId"] = userContext.UserId ?? "anonymous",
        ["HasCustomPrompt"] = !string.IsNullOrWhiteSpace(request.CustomPromptTemplate),
        ["PromptMode"] = request.PromptMode.ToString()
      });

      // Resolve CV text from CvId or use direct CvText
      var cvText = await ResolveCvTextAsync(request, cancellationToken);
      if (cvText.IsFailure)
      {
        logger.LogWarning("Failed to resolve CV text");
        return Result<GenerateCoverLetterResult>.Failure(cvText.Errors, cvText.Type);
      }

      // Fetch saved custom prompt from settings if available
      var savedCustomPrompt = await customPromptService.GetUserPromptAsync(PromptType.CoverLetter, cancellationToken);

      // Build prompt based on mode (Append or Override)
      var prompt = BuildPrompt(request, cvText.Value, savedCustomPrompt);

      // Check if user has saved their own API key (BYOK pattern)
      var userApiKey = userContext.GetUserApiKey();

      var options = new LlmGenerationOptions(
          SystemMessage: "You are a professional cover letter writer. Respond only with the cover letter text, no additional commentary.",
          ApiKey: userApiKey  // Use user's key if available, otherwise null (defaults to app's key)
      );

      var llmResponse = await llmService.GenerateAsync(prompt, options, cancellationToken);

      var result = new GenerateCoverLetterResult(
          CoverLetter: llmResponse.Content.Trim(),
          Model: llmResponse.Model,
          PromptTokens: llmResponse.PromptTokens,
          CompletionTokens: llmResponse.CompletionTokens,
          GeneratedAt: DateTime.UtcNow
      );

      logger.LogInformation(
          "Cover letter generated using {Model} - Tokens: {PromptTokens}→{CompletionTokens}",
          llmResponse.Model,
          llmResponse.PromptTokens,
          llmResponse.CompletionTokens);

      return Result.Success(result);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed to generate cover letter");
      return Result.Failure<GenerateCoverLetterResult>($"Failed to generate cover letter: {ex.Message}");
    }
  }

  /// <summary>
  /// Builds the final prompt based on the prompt mode (Append or Override).
  /// Priority: request.CustomPromptTemplate > savedCustomPrompt > default
  /// </summary>
  private string BuildPrompt(GenerateCoverLetterCommand request, string cvText, string? savedCustomPrompt)
  {
    var variables = new Dictionary<string, string>
    {
      { "JobDescription", request.JobDescription },
      { "CvText", cvText }
    };

    // Determine effective custom prompt (request overrides saved setting)
    var effectiveCustomPrompt = request.CustomPromptTemplate ?? savedCustomPrompt;

    if (string.IsNullOrWhiteSpace(effectiveCustomPrompt))
    {
      var promptResult = promptRegistry.GetPrompt(PromptType.CoverLetter, variables);
      return promptResult.IsSuccess ? promptResult.Value! : string.Empty;
    }

    if (request.PromptMode == PromptMode.Override)
    {
      // Override mode - use only custom template
      // Still allow for variable replacement if they use {JobDescription} or {CvText}
      var template = effectiveCustomPrompt;
      return variables.Aggregate(template, (current, variable) =>
          current.Replace("{" + variable.Key + "}", variable.Value));
    }

    // Append mode (default) - combine default + custom
    var basePromptResult = promptRegistry.GetPrompt(PromptType.CoverLetter, variables);
    if (basePromptResult.IsFailure)
      return string.Empty;

    return $"{basePromptResult.Value}\n\nADDITIONAL INSTRUCTIONS:\n{effectiveCustomPrompt}";
  }

  /// <summary>
  /// Resolves CV text from CvId (cache lookup) or uses direct CvText.
  /// Includes hyperlinks if available.
  /// </summary>
  private async Task<Result<string>> ResolveCvTextAsync(
      GenerateCoverLetterCommand request,
      CancellationToken cancellationToken)
  {
    // If CvId provided, retrieve from repository
    if (!string.IsNullOrWhiteSpace(request.CvId))
    {
      var cvResult = await cvRepository.GetByIdAsync(request.CvId, cancellationToken);
      if (cvResult.IsFailure)
      {
        return Result<string>.Failure(cvResult.Errors, cvResult.Type);
      }

      logger.LogDebug("Retrieved CV from repository: {CvId}", request.CvId);

      var cvText = cvResult.Value!.ExtractedText;

      // Append hyperlinks to CV text if available
      if (cvResult.Value.Hyperlinks != null && cvResult.Value.Hyperlinks.Any())
      {
        var linksList = new List<string>();
        foreach (var link in cvResult.Value.Hyperlinks)
        {
          var description = link.Type switch
          {
            HyperlinkType.Email => "Email",
            HyperlinkType.LinkedIn => "LinkedIn",
            HyperlinkType.GitHub => "GitHub",
            HyperlinkType.Portfolio => "Portfolio",
            _ => "Link"
          };
          linksList.Add($"- {description}: {link.Url}");
        }

        cvText += $"\n\nContact Links:\n{string.Join("\n", linksList)}";
      }

      return Result<string>.Success(cvText);
    }

    // Fallback to direct CvText (backward compatibility)
    if (!string.IsNullOrWhiteSpace(request.CvText))
    {
      return Result<string>.Success(request.CvText);
    }

    // Should never reach here due to validator, but defensive check
    return Result<string>.Failure(
        "Either CvId or CvText must be provided.",
        ResultType.InvalidInput);
  }
}
