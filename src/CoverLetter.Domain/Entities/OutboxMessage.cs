namespace CoverLetter.Domain.Entities;

/// <summary>
/// Outbox message implementing the transactional outbox pattern.
/// Written in the same DB transaction as the compile job it relates to,
/// then dispatched to the broker by a background service and marked sent.
/// </summary>
public class OutboxMessage
{
  public long Id { get; set; }
  public Guid MessageId { get; set; }
  public string Topic { get; set; } = string.Empty;
  public string Payload { get; set; } = string.Empty;
  public int Attempts { get; set; }
  public DateTime? DispatchedAt { get; set; }
  public DateTime? NextAttemptAt { get; set; }
  public DateTime CreatedAt { get; set; }
}
