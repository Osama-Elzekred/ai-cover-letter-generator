using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoverLetter.Infrastructure.Services;

/// <summary>
/// File-system backed <see cref="ICompileResultStorage"/>.
/// Stores each PDF as <c>{StoragePath}/{jobId}.pdf</c> on a mounted volume
/// shared between the API (for download) and the worker (for write).
/// </summary>
public sealed class FileCompileResultStorage(
    IOptions<CompileWorkerSettings> settings,
    ILogger<FileCompileResultStorage> logger) : ICompileResultStorage
{
    private readonly CompileWorkerSettings _settings = settings.Value;

    public async Task<string> WriteAsync(Guid jobId, byte[] content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_settings.StoragePath);
        var path = Path.Combine(_settings.StoragePath, $"{jobId}.pdf");
        await File.WriteAllBytesAsync(path, content, cancellationToken);
        logger.LogDebug("Wrote compile result {JobId} ({Bytes} bytes) to {Path}", jobId, content.Length, path);
        return path;
    }

    public Task<byte[]?> ReadAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_settings.StoragePath, $"{jobId}.pdf");
        if (!File.Exists(path))
            return Task.FromResult<byte[]?>(null);

        return File.ReadAllBytesAsync(path, cancellationToken).ContinueWith<byte[]?>(t => t.IsFaulted ? null : t.Result, cancellationToken);
    }
}
