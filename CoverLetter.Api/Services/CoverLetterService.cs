using CoverLetter.Api.Models;

namespace CoverLetter.Api.Services;

/// <summary>
/// Service for generating cover letters using LLM.
/// </summary>
public sealed class CoverLetterService : ICoverLetterService
{
  private readonly IGroqChatClient _groqClient;
  private readonly ILogger<CoverLetterService> _logger;

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

  public CoverLetterService(IGroqChatClient groqClient, ILogger<CoverLetterService> logger)
  {
    _groqClient = groqClient;
    _logger = logger;
  }

  public async Task<GenerateCoverLetterResponse> GenerateAsync(
      GenerateCoverLetterRequest request,
      CancellationToken cancellationToken = default)
  {
    _logger.LogInformation("Generating cover letter for job description of length {Length}",
        request.JobDescription.Length);

    var promptTemplate = request.CustomPromptTemplate ?? DefaultPromptTemplate;
    var prompt = string.Format(promptTemplate, request.JobDescription, request.CvText);

    var messages = new List<GroqMessage>
        {
            new("system", "You are a professional cover letter writer. Respond only with the cover letter text, no additional commentary, make the cover letter looks as human made as possible."),
            new("user", prompt)
        };

    var response = await _groqClient.ChatCompletionAsync(messages, cancellationToken);

    var coverLetterText = response.Choices.FirstOrDefault()?.Message.Content
        ?? throw new InvalidOperationException("No cover letter generated from LLM response.");

    _logger.LogInformation("Cover letter generated successfully");

    return new GenerateCoverLetterResponse(
        CoverLetter: coverLetterText.Trim(),
        Model: response.Model,
        PromptTokens: response.Usage.PromptTokens,
        CompletionTokens: response.Usage.CompletionTokens,
        GeneratedAt: DateTime.UtcNow
    );
  }
}
