namespace CoverLetter.Domain.Entities;

/// <summary>
/// Cover letter aggregate root - rich domain entity with behavior
/// </summary>
public class CoverLetterEntity
{
  public Guid Id { get; set; }
  public Guid UserId { get; set; }
  public Guid CvId { get; set; }
  public string JobDescription { get; set; } = string.Empty;
  public string? CompanyName { get; set; }
  public string Content { get; set; } = string.Empty;
  public string Status { get; set; } = "Draft"; // Draft, Generated, Published
  public DateTime CreatedAt { get; set; }
  public DateTime UpdatedAt { get; set; }
  public DateTime? PublishedAt { get; set; }

  // Navigation properties
  public Cv Cv { get; set; } = null!;

  // EF Core requires parameterless constructor
  public CoverLetterEntity() { }

  /// <summary>
  /// Factory method to create a new draft cover letter
  /// </summary>
  public static CoverLetterEntity CreateDraft(
      Guid userId,
      Guid cvId,
      string jobDescription,
      string? companyName = null)
  {
    return new CoverLetterEntity
    {
      Id = Guid.NewGuid(),
      UserId = userId,
      CvId = cvId,
      JobDescription = jobDescription,
      CompanyName = companyName,
      Content = string.Empty,
      Status = "Draft",
      CreatedAt = DateTime.UtcNow,
      UpdatedAt = DateTime.UtcNow
    };
  }

  /// <summary>
  /// Update content after generation
  /// </summary>
  public void SetContent(string content)
  {
    Content = content;
    Status = "Generated";
    UpdatedAt = DateTime.UtcNow;
  }

  /// <summary>
  /// Publish the cover letter
  /// </summary>
  public void Publish()
  {
    if (string.IsNullOrWhiteSpace(Content))
      throw new InvalidOperationException("Cannot publish a cover letter without content");

    Status = "Published";
    PublishedAt = DateTime.UtcNow;
    UpdatedAt = DateTime.UtcNow;
  }
}
