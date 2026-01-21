namespace CoverLetter.Domain.Entities;

/// <summary>
/// Prompt template entity for LLM prompts
/// </summary>
public class PromptTemplate
{
  public Guid Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string Template { get; set; } = string.Empty;
  public bool IsDefault { get; set; }
  public bool IsActive { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime UpdatedAt { get; set; }
}
