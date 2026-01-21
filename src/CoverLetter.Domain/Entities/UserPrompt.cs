using CoverLetter.Domain.Enums;

namespace CoverLetter.Domain.Entities;

/// <summary>
/// User custom prompt with rich domain behavior
/// </summary>
public class UserPrompt
{
  private UserPrompt() { } // EF Core constructor

  public Guid Id { get; private set; }
  public Guid UserId { get; private set; }
  public PromptType PromptType { get; private set; }
  public string Content { get; private set; } = string.Empty;
  public DateTime CreatedAt { get; private set; }
  public DateTime UpdatedAt { get; private set; }

  /// <summary>
  /// Factory method to create a new user prompt
  /// </summary>
  public static UserPrompt Create(Guid userId, PromptType promptType, string content)
  {
    if (userId == Guid.Empty)
      throw new ArgumentException("User ID cannot be empty", nameof(userId));
    if (string.IsNullOrWhiteSpace(content))
      throw new ArgumentException("Prompt content is required", nameof(content));

    var now = DateTime.UtcNow;
    return new UserPrompt
    {
      Id = Guid.NewGuid(),
      UserId = userId,
      PromptType = promptType,
      Content = content,
      CreatedAt = now,
      UpdatedAt = now
    };
  }

  public void Update(string content)
  {
    if (string.IsNullOrWhiteSpace(content))
      throw new ArgumentException("Prompt content is required", nameof(content));

    Content = content;
    UpdatedAt = DateTime.UtcNow;
  }
}
