namespace CoverLetter.Domain.Entities;

/// <summary>
/// Represents a generated cover letter entity.
/// </summary>
public sealed class CoverLetterEntity
{
  public Guid Id { get; private set; }
  public string JobDescription { get; private set; } = string.Empty;
  public string CvText { get; private set; } = string.Empty;
  public string GeneratedContent { get; private set; } = string.Empty;
  public string ModelUsed { get; private set; } = string.Empty;
  public int PromptTokens { get; private set; }
  public int CompletionTokens { get; private set; }
  public DateTime CreatedAt { get; private set; }

  private CoverLetterEntity() { } // EF Core

  public static CoverLetterEntity Create(
      string jobDescription,
      string cvText,
      string generatedContent,
      string modelUsed,
      int promptTokens,
      int completionTokens)
  {
    return new CoverLetterEntity
    {
      Id = Guid.NewGuid(),
      JobDescription = jobDescription,
      CvText = cvText,
      GeneratedContent = generatedContent,
      ModelUsed = modelUsed,
      PromptTokens = promptTokens,
      CompletionTokens = completionTokens,
      CreatedAt = DateTime.UtcNow
    };
  }
}
