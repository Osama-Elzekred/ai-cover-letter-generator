namespace CoverLetter.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for the RabbitMQ broker used by the compile outbox/worker pipeline.
/// Bound from the "RabbitMq" configuration section.
/// </summary>
public sealed class RabbitMqSettings
{
  public const string SectionName = "RabbitMq";

  /// <summary>
  /// Broker hostname.
  /// </summary>
  public required string Host { get; init; } = "localhost";

  /// <summary>
  /// AMQP port (default 5672).
  /// </summary>
  public int Port { get; init; } = 5672;

  /// <summary>
  /// Username for authentication.
  /// </summary>
  public required string UserName { get; init; } = "guest";

  /// <summary>
  /// Password for authentication.
  /// </summary>
  public required string Password { get; init; } = "guest";

  /// <summary>
  /// Virtual host (default "/").
  /// </summary>
  public string VirtualHost { get; init; } = "/";

  /// <summary>
  /// Exchange to publish compile job messages to.
  /// </summary>
  public string Exchange { get; init; } = "compile-jobs";

  /// <summary>
  /// Durable queue consuming compile job messages.
  /// </summary>
  public string Queue { get; init; } = "compile.jobs";
}
