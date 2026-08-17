using System.Text.Json;
using CoverLetter.Application.Common.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Infrastructure.Messaging;

public sealed class MassTransitCompileMessagePublisher : ICompileMessagePublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<MassTransitCompileMessagePublisher> _logger;

    public MassTransitCompileMessagePublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<MassTransitCompileMessagePublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task PublishAsync(Guid messageId, string topic, string payload, CancellationToken cancellationToken = default)
    {
        var message = JsonSerializer.Deserialize<CompileJobMessage>(payload, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize compile job payload.");

        await _publishEndpoint.Publish(message, context =>
        {
            context.MessageId = messageId;
            context.Headers.Set("X-Compile-Topic", topic);
        }, cancellationToken);

        _logger.LogDebug("Published compile job {MessageId} to MassTransit exchange", messageId);
    }
}
