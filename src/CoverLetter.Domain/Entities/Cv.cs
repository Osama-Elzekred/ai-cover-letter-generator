namespace CoverLetter.Domain.Entities;

/// <summary>
/// CV aggregate root with rich domain behavior
/// </summary>
public class Cv
{
  private Cv() { } // EF Core constructor

  public Guid Id { get; private set; }
  public Guid UserId { get; private set; }
  public string FileName { get; private set; } = string.Empty;
  public string Content { get; private set; } = string.Empty;
  public string? FileStoragePath { get; private set; }
  public bool IsActive { get; private set; }
  public DateTime CreatedAt { get; private set; }
  public DateTime UpdatedAt { get; private set; }

  // Navigation properties
  public ICollection<CoverLetterEntity> CoverLetters { get; private set; } = new List<CoverLetterEntity>();

  /// <summary>
  /// Factory method to create a new CV.
  /// Domain owns identity generation - ID is created here, not by caller.
  /// </summary>
  public static Cv Create(Guid userId, string fileName, string content)
  {
    if (userId == Guid.Empty)
      throw new ArgumentException("User ID cannot be empty", nameof(userId));
    if (string.IsNullOrWhiteSpace(fileName))
      throw new ArgumentException("File name is required", nameof(fileName));
    if (string.IsNullOrWhiteSpace(content))
      throw new ArgumentException("Content is required", nameof(content));

    var now = DateTime.UtcNow;
    return new Cv
    {
      Id = Guid.NewGuid(), // Domain generates identity
      UserId = userId,
      FileName = fileName,
      Content = content,
      IsActive = true,
      CreatedAt = now,
      UpdatedAt = now
    };
  }

  public void Update(string content)
  {
    if (string.IsNullOrWhiteSpace(content))
      throw new ArgumentException("Content is required", nameof(content));

    Content = content;
    UpdatedAt = DateTime.UtcNow;
  }

  public void Deactivate()
  {
    IsActive = false;
    UpdatedAt = DateTime.UtcNow;
  }
}
