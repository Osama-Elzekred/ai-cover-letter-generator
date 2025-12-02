using Asp.Versioning;

namespace CoverLetter.Api.Endpoints;

/// <summary>
/// Health check endpoints (version-neutral).
/// </summary>
public static class HealthEndpoints
{
  public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
  {
    var apiVersionSet = app.NewApiVersionSet()
        .HasApiVersion(new ApiVersion(1, 0))
        .Build();

    // Health endpoint accessible at /health (no version prefix needed)
    app.MapGet("/health", () => Results.Ok(new
    {
      Status = "Healthy",
      Timestamp = DateTime.UtcNow
    }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .Produces<object>(StatusCodes.Status200OK);

    return app;
  }
}
