using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.UseCases.GenerateCoverLetter;

/// <summary>
/// Handler for GenerateCoverLetterCommand.
/// This is where the business logic lives.
/// </summary>
public sealed class GenerateCoverLetterHandler
    : IRequestHandler<GenerateCoverLetterCommand, Result<GenerateCoverLetterResult>>
{
  private readonly ILlmService _llmService;
  private readonly ILogger<GenerateCoverLetterHandler> _logger;

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
        
        JOB DESCRIPTION:
        {0}
        
        CANDIDATE'S CV:
        {1}
        
        Write the cover letter now:
        """;

  public GenerateCoverLetterHandler(
      ILlmService llmService,
      ILogger<GenerateCoverLetterHandler> logger)
  {
    _llmService = llmService;
    _logger = logger;
  }

  public async Task<Result<GenerateCoverLetterResult>> Handle(
      GenerateCoverLetterCommand request,
      CancellationToken cancellationToken)
  {
    try
    {
      var promptTemplate = request.CustomPromptTemplate ?? DefaultPromptTemplate;
      var prompt = string.Format(promptTemplate, request.JobDescription, request.CvText);

      var llmResponse = await _llmService.GenerateAsync(prompt, cancellationToken);

      var result = new GenerateCoverLetterResult(
          CoverLetter: llmResponse.Content.Trim(),
          Model: llmResponse.Model,
          PromptTokens: llmResponse.PromptTokens,
          CompletionTokens: llmResponse.CompletionTokens,
          GeneratedAt: DateTime.UtcNow
      );

      _logger.LogInformation(
          "Cover letter generated using {Model} - Tokens: {PromptTokens}→{CompletionTokens}",
          llmResponse.Model,
          llmResponse.PromptTokens,
          llmResponse.CompletionTokens);

      return Result.Success(result);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to generate cover letter");
      return Result.Failure<GenerateCoverLetterResult>($"Failed to generate cover letter: {ex.Message}");
    }
  }
}
