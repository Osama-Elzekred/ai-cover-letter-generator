using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Enums;
using MediatR;
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
          ApiKey: userApiKey
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
  /// Builds the prompt sent to the LLM.
  /// Override mode: inline template replaces everything for this call only.
  /// Append mode / no inline: base is saved prompt if set, otherwise default registry.
  /// </summary>
  private string BuildPrompt(GenerateCoverLetterCommand request, string cvText, string? savedCustomPrompt)
  {
    var variables = new Dictionary<string, string>
    {
      { "JobDescription", request.JobDescription },
      { "CvText", cvText }
    };

    string Resolve(string template) =>
        variables.Aggregate(template, (c, kv) => c.Replace("{" + kv.Key + "}", kv.Value));

    // ── Override mode: inline replaces everything for this call only ─────
    if (!string.IsNullOrWhiteSpace(request.CustomPromptTemplate) && request.PromptMode == PromptMode.Override)
    {
      var resolved = Resolve(request.CustomPromptTemplate);
      // Inject any missing context so the LLM always has what it needs
      if (!request.CustomPromptTemplate.Contains("{JobDescription}"))
        resolved += $"\n\nJOB DESCRIPTION:\n{request.JobDescription}";
      if (!request.CustomPromptTemplate.Contains("{CvText}"))
        resolved += $"\n\nCANDIDATE'S CV (use this for all personal details, name, and sign-off):\n{cvText}";
      return resolved;
    }

    // ── Base = saved prompt if exists, otherwise default registry ────────
    string basePrompt;
    if (!string.IsNullOrWhiteSpace(savedCustomPrompt))
      basePrompt = Resolve(savedCustomPrompt);
    else
    {
      var baseResult = promptRegistry.GetPrompt(PromptType.CoverLetter, variables);
      if (baseResult.IsFailure) return string.Empty;
      basePrompt = baseResult.Value!;
    }

    // ── Append mode: add inline instructions on top of the base ─────────
    if (!string.IsNullOrWhiteSpace(request.CustomPromptTemplate))
      return $"{basePrompt}\n\nADDITIONAL INSTRUCTIONS:\n{Resolve(request.CustomPromptTemplate)}";

    return basePrompt;
  }

  /// <summary>
  /// Resolves CV text from CvId (repository lookup) or uses direct CvText.
  /// Includes hyperlinks if available.
  /// </summary>
  private async Task<Result<string>> ResolveCvTextAsync(
      GenerateCoverLetterCommand request,
      CancellationToken cancellationToken)
  {
    // If CvId provided, retrieve from repository
    if (!string.IsNullOrWhiteSpace(request.CvId))
    {
      if (!Guid.TryParse(request.CvId, out var cvId))
      {
        return Result<string>.Failure("Invalid CvId format", ResultType.InvalidInput);
      }

      var cv = await cvRepository.GetByIdAsync(cvId, cancellationToken);
      if (cv is null)
      {
        return Result<string>.Failure($"CV not found: {request.CvId}", ResultType.NotFound);
      }

      logger.LogDebug("Retrieved CV from repository: {CvId}", request.CvId);

      return Result<string>.Success(cv.Content);
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