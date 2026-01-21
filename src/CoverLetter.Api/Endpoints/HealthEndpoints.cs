using Asp.Versioning;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoverLetter.Api.Endpoints;

/// <summary>
/// Health check endpoints (version-neutral).
/// </summary>
public static class HealthEndpoints
{
  public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app, HealthCheckOptions? healthCheckOptions = null)
  {
    var apiVersionSet = app.NewApiVersionSet()
        .HasApiVersion(new ApiVersion(1, 0))
        .Build();

    app.MapGet("/health", () => Results.Ok(new
    {
      Status = "Healthy",
      Timestamp = DateTime.UtcNow
    }))
    .WithSummary("Check API health")
    .WithDescription("Returns the health status of the API")
    .WithTags("Health")
    .Produces<object>(StatusCodes.Status200OK);

    app.MapGet("/health/ready", async (HealthCheckService healthCheckService) =>
    {
      try
      {
        var report = await healthCheckService.CheckHealthAsync(check => check.Tags.Contains("dependency"));

        var response = new
        {
          status = report.Status.ToString(),
          checks = report.Entries.Select(e => new
          {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description
          })
        };

        // Return 503 if any dependency is unhealthy; otherwise 200
        return report.Status == HealthStatus.Healthy
          ? Results.Ok(response)
          : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
      }
      catch
      {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
      }
    })
    .WithSummary("Check API dependencies (readiness probe)")
    .WithDescription("Returns 200 if all dependencies are healthy, 503 otherwise. Used by orchestrators for rolling restarts.")
    .WithTags("Health")
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status503ServiceUnavailable);

    return app;
  }
}
