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
/// </summary>
public sealed class GenerateCoverLetterHandler(
    ILlmService llmService,
    IMemoryCache cache,
    ILogger<GenerateCoverLetterHandler> logger)
    : IRequestHandler<GenerateCoverLetterCommand, Result<GenerateCoverLetterResult>>
{
  private const string DefaultPromptTemplate = """
        You are an expert career coach and professional cover letter writer.
        
        Your task is to write a compelling, personalized cover letter based on:
        1. The job description provided
        2. The candidate's CV/resume
        
        Guidelines:
        - Write in a professional yet engaging tone
        - Highlight relevant experience and skills that match the job requirements
        - Show enthusiasm for the role and company
        - Keep it concise (3-4 paragraphs)
        - Include a strong opening that grabs attention
        - Connect the candidate's achievements to the job requirements
        - End with a clear call to action
        - Do NOT include placeholder text like [Company Name] - if unknown, write generically
        - Do NOT make up information not present in the CV
        - Make it look human-written and avoid AI-detection triggers
        
        JOB DESCRIPTION:
        {0}
        
        CANDIDATE'S CV:
        {1}
        
        Write the cover letter now:
        """;

  public async Task<Result<GenerateCoverLetterResult>> Handle(
      GenerateCoverLetterCommand request,
      CancellationToken cancellationToken)
  {
    try
    {
      // Resolve CV text from CvId or use direct CvText
      var cvText = await ResolveCvTextAsync(request, cancellationToken);
      if (cvText.IsFailure)
      {
        return Result<GenerateCoverLetterResult>.Failure(cvText.Errors, cvText.Type);
      }

      // Build prompt based on mode (Append or Override)
      var prompt = BuildPrompt(request, cvText.Value);

      var options = new LlmGenerationOptions(
          SystemMessage: "You are a professional cover letter writer. Respond only with the cover letter text, no additional commentary."
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
  /// </summary>
  private string BuildPrompt(GenerateCoverLetterCommand request, string cvText)
  {
    if (string.IsNullOrWhiteSpace(request.CustomPromptTemplate))
    {
      // No custom template - use default
      return string.Format(DefaultPromptTemplate, request.JobDescription, cvText);
    }

    if (request.PromptMode == PromptMode.Override)
    {
      // Override mode - use only custom template
      return string.Format(request.CustomPromptTemplate, request.JobDescription, cvText);
    }

    // Append mode (default) - combine default + custom
    var basePrompt = string.Format(DefaultPromptTemplate, request.JobDescription, cvText);
    return $"{basePrompt}\n\nADDITIONAL INSTRUCTIONS:\n{request.CustomPromptTemplate}";
  }

  /// <summary>
  /// Resolves CV text from CvId (cache lookup) or uses direct CvText.
  /// </summary>
  private Task<Result<string>> ResolveCvTextAsync(
      GenerateCoverLetterCommand request,
      CancellationToken cancellationToken)
  {
    // If CvId provided, retrieve from cache
    if (!string.IsNullOrWhiteSpace(request.CvId))
    {
      var cacheKey = $"cv:{request.CvId}";

      if (!cache.TryGetValue<CvDocument>(cacheKey, out var cvDocument) || cvDocument is null)
      {
        logger.LogWarning("CV not found in cache: {CvId}", request.CvId);
        return Task.FromResult(Result<string>.Failure(
            $"CV with ID '{request.CvId}' not found. The CV may have expired (24h cache) or the ID is invalid.",
            ResultType.NotFound));
      }

      logger.LogDebug("Retrieved CV from cache: {CvId}", request.CvId);
      return Task.FromResult(Result<string>.Success(cvDocument.ExtractedText));
    }

    // Fallback to direct CvText (backward compatibility)
    if (!string.IsNullOrWhiteSpace(request.CvText))
    {
      return Task.FromResult(Result<string>.Success(request.CvText));
    }

    // Should never reach here due to validator, but defensive check
    return Task.FromResult(Result<string>.Failure(
        "Either CvId or CvText must be provided.",
        ResultType.InvalidInput));
  }
}
