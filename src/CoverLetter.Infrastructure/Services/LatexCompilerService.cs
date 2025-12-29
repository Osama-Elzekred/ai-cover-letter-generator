using System.Diagnostics;
using CoverLetter.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Infrastructure.Services;

/// <summary>
/// Implementation of ILatexCompilerService using Docker.
/// Spawns a container to compile LaTeX source to PDF in a sandboxed environment.
/// </summary>
public sealed class LatexCompilerService(ILogger<LatexCompilerService> logger) : ILatexCompilerService
{
    private const string DockerImageName = "latexmk-compiler:1";
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    public async Task<byte[]> CompileToPdfAsync(string latexSource, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var workspaceDir = Path.Combine(Path.GetTempPath(), "cv-gen", jobId);
        
        try
        {
            Directory.CreateDirectory(workspaceDir);
            var mainTexPath = Path.Combine(workspaceDir, "main.tex");
            await File.WriteAllTextAsync(mainTexPath, latexSource, cancellationToken);

            var outDir = Path.Combine(workspaceDir, "out");
            Directory.CreateDirectory(outDir);

            logger.LogInformation("Starting LaTeX compilation for job {JobId}", jobId);

            // workspaceDir mapping to /work in container
            // We use absolute path for mounting
            var absoluteWorkspaceDir = Path.GetFullPath(workspaceDir);

            // docker run flags for security and resource limits
            var args = 
                "run --rm " +
                "--network none " +
                "--memory=512m --cpus=1.0 --pids-limit=256 " +
                "--read-only --tmpfs /tmp:rw,size=64m " +
                $"-v \"{absoluteWorkspaceDir}:/work:rw\" " +
                DockerImageName;

            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start Docker process.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                throw new TimeoutException($"LaTeX compilation timed out after {_timeout.TotalSeconds} seconds.");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                var logPath = Path.Combine(outDir, "main.log");
                string? latexLog = null;
                if (File.Exists(logPath))
                {
                    latexLog = await File.ReadAllTextAsync(logPath, cancellationToken);
                }

                logger.LogError("LaTeX compilation failed for job {JobId}. Exit code: {ExitCode}\nStderr: {Stderr}\nLog: {Log}", 
                    jobId, process.ExitCode, stderr, latexLog);
                
                throw new Exception($"LaTeX compilation failed (exit {process.ExitCode}). {stderr}");
            }

            var outPdfPath = Path.Combine(outDir, "main.pdf");
            if (!File.Exists(outPdfPath))
            {
                throw new FileNotFoundException("PDF was not generated despite successful exit code.");
            }

            return await File.ReadAllBytesAsync(outPdfPath, cancellationToken);
        }
        finally
        {
            // Cleanup temp directory
            try
            {
                if (Directory.Exists(workspaceDir))
                {
                    Directory.Delete(workspaceDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to cleanup LaTeX workspace directory: {WorkspaceDir}", workspaceDir);
            }
        }
    }
}
