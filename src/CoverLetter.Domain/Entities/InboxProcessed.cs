namespace CoverLetter.Domain.Entities;

/// <summary>
/// Inbox deduplication record consumed by the worker to safely implement
/// at-least-once delivery. A message id present here means the job has
/// already been processed and the redelivery must be skipped.
/// </summary>
public class InboxProcessed
{
  public Guid MessageId { get; set; }
  public DateTime ProcessedAt { get; set; }
  public Guid? JobId { get; set; }
}
