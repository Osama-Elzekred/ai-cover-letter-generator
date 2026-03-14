using CoverLetter.Domain.Enums;

namespace CoverLetter.Domain.Entities;

/// <summary>
/// Persisted user-specific API key for BYOK.
/// </summary>
public class UserApiKey
{
  private UserApiKey() { } // EF Core

  public Guid Id { get; private set; }
  public string UserId { get; private set; } = string.Empty;
  public LlmProvider Provider { get; private set; }
  public string ApiKey { get; private set; } = string.Empty;
  public DateTime CreatedAt { get; private set; }
  public DateTime UpdatedAt { get; private set; }

  public static UserApiKey Create(string userId, LlmProvider provider, string apiKey)
  {
    if (string.IsNullOrWhiteSpace(userId))
      throw new ArgumentException("User ID is required", nameof(userId));
    if (string.IsNullOrWhiteSpace(apiKey))
      throw new ArgumentException("API key is required", nameof(apiKey));

    var now = DateTime.UtcNow;
    return new UserApiKey
    {
      Id = Guid.NewGuid(),
      UserId = userId,
      Provider = provider,
      ApiKey = apiKey,
      CreatedAt = now,
      UpdatedAt = now
    };
  }

  public void Update(string apiKey)
  {
    if (string.IsNullOrWhiteSpace(apiKey))
      throw new ArgumentException("API key is required", nameof(apiKey));

    ApiKey = apiKey;
    UpdatedAt = DateTime.UtcNow;
  }
}
