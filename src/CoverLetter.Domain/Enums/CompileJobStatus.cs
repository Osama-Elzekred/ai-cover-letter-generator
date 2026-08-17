namespace CoverLetter.Domain.Enums;

/// <summary>
/// Lifecycle states of a LaTeX compile job.
/// </summary>
public enum CompileJobStatus
{
  /// <summary>
  /// Job has been enqueued and is waiting to be picked up by a worker.
  /// </summary>
  Pending,

  /// <summary>
  /// A worker is currently compiling the LaTeX source.
  /// </summary>
  Processing,

  /// <summary>
  /// Compilation finished and the resulting PDF is available for download.
  /// </summary>
  Completed,

  /// <summary>
  /// Compilation failed (e.g. invalid LaTeX, timeout). The error field holds the reason.
  /// </summary>
  Failed,

  /// <summary>
  /// Job was cancelled before completion.
  /// </summary>
  Cancelled
}
