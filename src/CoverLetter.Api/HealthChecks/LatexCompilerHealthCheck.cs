using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoverLetter.Api.HealthChecks;

/// <summary>
/// Health check that verifies the LaTeX compiler (pdflatex) is available and functional.
/// </summary>
public sealed class LatexCompilerHealthCheck : IHealthCheck
{
  private readonly ILogger<LatexCompilerHealthCheck> _logger;

  public LatexCompilerHealthCheck(ILogger<LatexCompilerHealthCheck> logger)
  {
    _logger = logger;
  }

  public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
  {
    try
    {
      // Check if pdflatex is available by running --version
      using var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "pdflatex",
          Arguments = "--version",
          RedirectStandardOutput = true,
          UseShellExecute = false,
          CreateNoWindow = true
        }
      };

      process.Start();
      var completed = await Task.Run(() => process.WaitForExit(5000), cancellationToken);

      if (!completed)
      {
        process.Kill();
        return HealthCheckResult.Unhealthy("pdflatex command timed out");
      }

      if (process.ExitCode == 0)
      {
        _logger.LogDebug("LaTeX compiler health check passed");
        return HealthCheckResult.Healthy("LaTeX compiler (pdflatex) is available");
      }

      return HealthCheckResult.Unhealthy($"pdflatex exited with code {process.ExitCode}");
    }
    catch (FileNotFoundException)
    {
      var message = "pdflatex not found. Install TeX Live or MikTeX to enable CV compilation.";
      _logger.LogWarning(message);
      return HealthCheckResult.Unhealthy(message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "LaTeX compiler health check failed");
      return HealthCheckResult.Unhealthy("LaTeX compiler check failed", ex);
    }
  }
}
