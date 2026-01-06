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
      var report = await healthCheckService.CheckHealthAsync(check => check.Tags.Contains("dependency"));
      return Results.Ok(new
      {
        status = report.Status.ToString(),
        checks = report.Entries.Select(e => new
        {
          name = e.Key,
          status = e.Value.Status.ToString(),
          description = e.Value.Description
        })
      });
    })
    .WithSummary("Check API dependencies")
    .WithTags("Health");

    return app;
  }
}
