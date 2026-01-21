using CoverLetter.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoverLetter.Api.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
  private readonly AppDbContext _context;

  public DatabaseHealthCheck(AppDbContext context)
  {
    _context = context;
  }

  public async Task<HealthCheckResult> CheckHealthAsync(
      HealthCheckContext context,
      CancellationToken cancellationToken = default)
  {
    try
    {
      // Simple query to check DB connectivity
      await _context.Database.CanConnectAsync(cancellationToken);
      return HealthCheckResult.Healthy("Database is reachable");
    }
    catch (Exception ex)
    {
      return HealthCheckResult.Unhealthy(
          "Database is unreachable",
          exception: ex);
    }
  }
}
