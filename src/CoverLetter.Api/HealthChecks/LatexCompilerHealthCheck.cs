using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CoverLetter.Api.HealthChecks;

/// <summary>
/// Health check that verifies the LaTeX compiler (pdflatex) is available and functional.
/// </summary>
public sealed class LatexCompilerHealthCheck : IHealthCheck
{
  private readonly ILogger<LatexCompilerHealthCheck> _logger;
  private readonly IWebHostEnvironment _environment;

  public LatexCompilerHealthCheck(ILogger<LatexCompilerHealthCheck> logger, IWebHostEnvironment environment)
  {
    _logger = logger;
    _environment = environment;
  }

  public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
  {
    try
    {
      // Check if Docker is running and the LaTeX image exists
      using var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "docker",
          Arguments = "images latexmk-compiler:1 --format \"{{.Repository}}:{{.Tag}}\"",
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          UseShellExecute = false,
          CreateNoWindow = true
        }
      };

      process.Start();
      var completed = await Task.Run(() => process.WaitForExit(5000), cancellationToken);

      if (!completed)
      {
        process.Kill();
        return HealthCheckResult.Unhealthy("Docker command timed out");
      }

      var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
      var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);

      if (process.ExitCode != 0)
      {
        _logger.LogWarning("Docker check failed: {Stderr}", stderr);

        if (_environment.IsDevelopment())
        {
          return HealthCheckResult.Healthy("LaTeX compiler not configured (OK in local dev - build Docker image for PDF generation)");
        }

        return HealthCheckResult.Degraded("Docker is not running or LaTeX image is missing");
      }

      // Check if the image name appears in output
      if (output.Contains("latexmk-compiler:1"))
      {
        _logger.LogDebug("LaTeX compiler health check passed - Docker image found");
        return HealthCheckResult.Healthy("LaTeX compiler Docker image (latexmk-compiler:1) is available");
      }

      _logger.LogWarning("LaTeX Docker image 'latexmk-compiler:1' not found");

      if (_environment.IsDevelopment())
      {
        return HealthCheckResult.Healthy("LaTeX Docker image not built yet (OK in local dev - run: docker build -t latexmk-compiler:1 .)");
      }

      return HealthCheckResult.Degraded("LaTeX Docker image 'latexmk-compiler:1' not found. Build the image to enable PDF generation.");
    }
    catch (FileNotFoundException)
    {
      var message = "Docker not found. Install Docker Desktop to enable CV compilation.";
      _logger.LogWarning(message);

      if (_environment.IsDevelopment())
      {
        return HealthCheckResult.Healthy("Docker not installed (OK in local dev)");
      }

      return HealthCheckResult.Degraded(message);
    }
    catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
    {
      // File not found - pdflatex not installed or not in PATH
      if (_environment.IsDevelopment())
      {
        _logger.LogDebug("pdflatex not found (expected in local development without Docker)");
        return HealthCheckResult.Healthy("LaTeX compiler not available (OK in local dev - run Docker for PDF generation)");
      }

      _logger.LogWarning("pdflatex not found in PATH - LaTeX functionality will be unavailable");
      return HealthCheckResult.Degraded("LaTeX compiler (pdflatex) not found. Ensure Docker/LaTeX container is running.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "LaTeX compiler health check failed");
      return HealthCheckResult.Unhealthy("LaTeX compiler check failed", ex);
    }
  }
}
