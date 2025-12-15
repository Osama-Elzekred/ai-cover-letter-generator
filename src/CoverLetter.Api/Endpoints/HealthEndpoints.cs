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

    app.MapGet("/health", () => Results.Ok(new
    {
      Status = "Healthy",
      Timestamp = DateTime.UtcNow
    }))
    .WithSummary("Check API health")
    .WithDescription("Returns the health status of the API")
    .WithTags("Health")
    .Produces<object>(StatusCodes.Status200OK);

    return app;
  }
}
