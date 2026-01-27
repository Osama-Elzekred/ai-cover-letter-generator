using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.UseCases.AnswerTextareaQuestion;

/// <summary>
/// Handler for AnswerQuestionCommand.
/// Generates short, focused answers to job application textarea questions
/// using the user's CV information and optional job context.
/// Supports custom prompt templates via user settings.
/// </summary>
public sealed class AnswerQuestionHandler(
    ILlmService llmService,
    ICvRepository cvRepository,
    IUserContext userContext,
    IPromptRegistry promptRegistry,
    ILogger<AnswerQuestionHandler> logger)
    : IRequestHandler<AnswerQuestionCommand, Result<AnswerQuestionResult>>
{
  public async Task<Result<AnswerQuestionResult>> Handle(
      AnswerQuestionCommand request,
      CancellationToken cancellationToken)
  {
    try
    {
      using var scope = logger.BeginScope(new Dictionary<string, object>
      {
        ["UserId"] = userContext.UserId ?? "anonymous",
        ["FieldLabel"] = request.FieldLabel,
        ["HasJobContext"] = !string.IsNullOrWhiteSpace(request.JobDescription)
      });

      // Resolve CV text from CvId
      var cvResult = await ResolveCvTextAsync(request.CvId, cancellationToken);
      if (cvResult.IsFailure)
      {
        logger.LogWarning("Failed to resolve CV text");
        return Result<AnswerQuestionResult>.Failure(cvResult.Errors, cvResult.Type);
      }

      // Build the prompt for generating a focused answer
      var prompt = BuildAnswerPrompt(request, cvResult.Value);

      // Check if user has saved their own API key (BYOK pattern)
      var userApiKey = userContext.GetUserApiKey();

      var options = new LlmGenerationOptions(
          SystemMessage: "You are an expert resume writer and career coach. Generate a concise, professional answer to the given question using the provided CV information. Keep responses between 50-500 characters. Be direct and relevant. Respond only with the answer text, no additional commentary.",
          ApiKey: userApiKey
      );

      var llmResponse = await llmService.GenerateAsync(prompt, options, cancellationToken);

      var result = new AnswerQuestionResult(
          Answer: llmResponse.Content.Trim()
      );

      logger.LogInformation(
          "Textarea answer generated for field '{FieldLabel}' using {Model} - Tokens: {PromptTokens}→{CompletionTokens}",
          request.FieldLabel,
          llmResponse.Model,
          llmResponse.PromptTokens,
          llmResponse.CompletionTokens);

      return Result.Success(result);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed to generate textarea answer");
      return Result.Failure<AnswerQuestionResult>($"Failed to generate answer: {ex.Message}");
    }
  }

  /// <summary>
  /// Builds the prompt for generating a focused textarea answer.
  /// Uses custom prompt template if available, otherwise uses default.
  /// </summary>
  private string BuildAnswerPrompt(AnswerQuestionCommand request, string cvText)
  {
    // Determine effective prompt template
    var effectiveCustomPrompt = request.CustomPromptTemplate;
    if (string.IsNullOrWhiteSpace(effectiveCustomPrompt))
    {
      // Use default from registry
      var promptResult = promptRegistry.GetPrompt(PromptType.TextareaAnswer, new Dictionary<string, string>
            {
                { "FieldLabel", request.FieldLabel },
                { "UserQuestion", request.UserQuestion },
                { "CvText", cvText }
            });
      return promptResult.IsSuccess ? promptResult.Value! : BuildDefaultAnswerPrompt(request, cvText);
    }

    // Custom template provided - replace variables
    var variables = new Dictionary<string, string>
        {
            { "FieldLabel", request.FieldLabel },
            { "UserQuestion", request.UserQuestion },
            { "CvText", cvText }
        };

    // Add optional job context if provided
    if (!string.IsNullOrWhiteSpace(request.JobDescription))
    {
      variables["JobTitle"] = request.JobTitle ?? "";
      variables["CompanyName"] = request.CompanyName ?? "";
      variables["JobDescription"] = request.JobDescription;
    }

    var result = variables.Aggregate(effectiveCustomPrompt, (current, variable) =>
        current.Replace("{" + variable.Key + "}", variable.Value));

    return result;
  }

  /// <summary>
  /// Builds the default answer prompt when no custom template is provided.
  /// </summary>
  private string BuildDefaultAnswerPrompt(AnswerQuestionCommand request, string cvText)
  {
    var prompt = $@"You are answering a job application question.

Field: {request.FieldLabel}
Question: {request.UserQuestion}

Candidate CV:
{cvText}";

    // Add job context if provided
    if (!string.IsNullOrWhiteSpace(request.JobDescription))
    {
      prompt += $@"

Job Context:
Title: {request.JobTitle}
Company: {request.CompanyName}
Description:
{request.JobDescription}";
    }

    prompt += @"

Generate a concise, professional answer (50-500 characters) to the question using information from the CV and job context. Be direct and relevant. Do not include any preamble or explanation.";

    return prompt;
  }

  /// <summary>
  /// Resolves CV text from CvId repository lookup.
  /// </summary>
  private async Task<Result<string>> ResolveCvTextAsync(
      string cvId,
      CancellationToken cancellationToken)
  {
    if (!Guid.TryParse(cvId, out var cvIdGuid))
    {
      return Result<string>.Failure("Invalid CvId format", ResultType.InvalidInput);
    }

    var cv = await cvRepository.GetByIdAsync(cvIdGuid, cancellationToken);
    if (cv is null)
    {
      return Result<string>.Failure($"CV not found: {cvId}", ResultType.NotFound);
    }

    logger.LogDebug("Retrieved CV from repository: {CvId}", cvId);

    return Result<string>.Success(cv.Content);
  }
}
