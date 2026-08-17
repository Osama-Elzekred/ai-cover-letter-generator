using MassTransit;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoverLetter.Infrastructure.HealthChecks;

public sealed class MassTransitHealthCheck : IHealthCheck
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitHealthCheck(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // A lightweight check: ensure the publish endpoint is resolvable.
        // This avoids depending on internal MassTransit health types.
        return Task.FromResult(_publishEndpoint is not null
            ? HealthCheckResult.Healthy("Message publish endpoint available")
            : HealthCheckResult.Unhealthy("Message publish endpoint not available"));
    }
}
