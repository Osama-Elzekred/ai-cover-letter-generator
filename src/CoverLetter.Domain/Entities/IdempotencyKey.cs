namespace CoverLetter.Domain.Entities;

/// <summary>
/// Idempotency key for preventing duplicate operations
/// </summary>
public class IdempotencyKey
{
  public Guid Id { get; set; }
  public string Key { get; set; } = string.Empty;
  public Guid UserId { get; set; }
  public string RequestPath { get; set; } = string.Empty;
  public int StatusCode { get; set; }
  public string? ResponseBody { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime ExpiresAt { get; set; }
}
