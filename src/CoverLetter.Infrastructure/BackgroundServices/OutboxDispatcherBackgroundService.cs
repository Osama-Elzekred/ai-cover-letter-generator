using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Repositories;
using CoverLetter.Infrastructure.Configuration;
using CoverLetter.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoverLetter.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that scans the outbox for undispatched messages and publishes
/// them to RabbitMQ. On success the message is marked dispatched; on failure the
/// attempt counter is incremented with exponential backoff up to MaxOutboxAttempts.
/// </summary>
public sealed class OutboxDispatcherBackgroundService(
    IServiceScopeFactory scopeFactory,
    ICompileMessagePublisher publisher,
    IOptions<CompileWorkerSettings> settings,
    ILogger<OutboxDispatcherBackgroundService> logger)
    : BackgroundService
{
    private readonly CompileWorkerSettings _settings = settings.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox dispatcher started (poll={PollMs}ms, batch={Batch}, maxAttempts={Max})",
            _settings.OutboxPollIntervalMs, _settings.OutboxBatchSize, _settings.MaxOutboxAttempts);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Outbox dispatcher cycle failed");
            }

            try
            {
                await Task.Delay(_settings.OutboxPollIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Outbox dispatcher stopped");
    }

    private async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();

        var batch = await outbox.GetUndispatchedBatchAsync(_settings.OutboxBatchSize, cancellationToken);
        CompilePipelineMetrics.OutboxBacklog.Set(batch.Count);

        if (batch.Count == 0)
            return;

        logger.LogDebug("Dispatching {Count} outbox message(s)", batch.Count);

        foreach (var message in batch)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await publisher.PublishAsync(message.MessageId, message.Topic, message.Payload, cancellationToken);
                await outbox.MarkDispatchedAsync(message.Id, cancellationToken);
                CompilePipelineMetrics.OutboxDispatchTotal.WithLabels("success").Inc();
                logger.LogInformation("Dispatched outbox message {MessageId} (row {RowId})", message.MessageId, message.Id);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                var stillRetrying = await outbox.MarkFailedAttemptAsync(
                    message.Id, _settings.MaxOutboxAttempts, _settings.OutboxBackoffBaseSeconds, cancellationToken);

                CompilePipelineMetrics.OutboxDispatchTotal.WithLabels("failure").Inc();

                if (stillRetrying)
                    logger.LogWarning(ex, "Failed to dispatch outbox message {MessageId} (attempt {Attempt}); will retry",
                        message.MessageId, message.Attempts + 1);
                else
                    logger.LogCritical(ex, "Outbox message {MessageId} exhausted {Max} attempts and will not be retried",
                        message.MessageId, _settings.MaxOutboxAttempts);
            }
        }
    }
}
