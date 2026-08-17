using System.Text.Json;
using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Enums;
using CoverLetter.Infrastructure.Configuration;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoverLetter.Infrastructure.Messaging;

public sealed class CompileJobConsumer : IConsumer<CompileJobMessage>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxErrorLength = 2000;
    private readonly ILogger<CompileJobConsumer> _logger;
    private readonly ICompileJobRepository _compileJobs;
    private readonly IInboxProcessedRepository _inbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILatexCompilerService _compiler;
    private readonly ICompileResultStorage _storage;
    private readonly CompileWorkerSettings _workerSettings;

    public CompileJobConsumer(
        ILogger<CompileJobConsumer> logger,
        ICompileJobRepository compileJobs,
        IInboxProcessedRepository inbox,
        IUnitOfWork unitOfWork,
        ILatexCompilerService compiler,
        ICompileResultStorage storage,
        CompileWorkerSettings workerSettings)
    {
        _logger = logger;
        _compileJobs = compileJobs;
        _inbox = inbox;
        _unitOfWork = unitOfWork;
        _compiler = compiler;
        _storage = storage;
        _workerSettings = workerSettings;
    }

    public async Task Consume(ConsumeContext<CompileJobMessage> context)
    {
        var message = context.Message;
        var messageId = context.MessageId ?? Guid.Empty;

        if (messageId == Guid.Empty)
        {
            _logger.LogError("Received compile message without message id; dead-lettering");
            throw new InvalidOperationException("MessageId is required for compile job deduplication.");
        }

        if (await _inbox.ExistsAsync(messageId, context.CancellationToken))
        {
            _logger.LogDebug("Skipping already-processed message {MessageId} for job {JobId}", messageId, message.JobId);
            return;
        }

        var job = await _compileJobs.GetByIdAsync(message.JobId, context.CancellationToken);
        if (job is null)
        {
            _logger.LogError("Compile job {JobId} not found for message {MessageId}; dead-lettering", message.JobId, messageId);
            throw new InvalidOperationException($"Compile job {message.JobId} not found.");
        }

        if (job.Status is CompileJobStatus.Completed or CompileJobStatus.Failed or CompileJobStatus.Cancelled)
        {
            _logger.LogDebug("Job {JobId} already terminal ({Status}); skipping", job.Id, job.Status);
            return;
        }

        if (job.Status == CompileJobStatus.Pending)
        {
            await _compileJobs.MarkProcessingAsync(job.Id, context.CancellationToken);
            await _unitOfWork.SaveChangesAsync(context.CancellationToken);
        }

        var compileSw = System.Diagnostics.Stopwatch.StartNew();
        byte[] pdf;
        try
        {
            using var compileCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            compileCts.CancelAfter(TimeSpan.FromSeconds(_workerSettings.CompileTimeoutSeconds));
            pdf = await _compiler.CompileToPdfAsync(message.Latex, compileCts.Token);
        }
        catch (TimeoutException ex)
        {
            compileSw.Stop();
            _logger.LogWarning(ex, "Compile timed out for job {JobId}; retrying", job.Id);
            throw;
        }
        catch (Exception ex)
        {
            compileSw.Stop();
            await _compileJobs.MarkFailedAsync(job.Id, Truncate(ex.Message), context.CancellationToken);
            await RecordInboxAsync(messageId, job.Id, context.CancellationToken);
            _logger.LogError(ex, "Compile failed for job {JobId}", job.Id);
            return;
        }

        compileSw.Stop();

        var resultPath = await _storage.WriteAsync(job.Id, pdf, context.CancellationToken);
        await _compileJobs.MarkCompletedAsync(job.Id, resultPath, context.CancellationToken);
        await RecordInboxAsync(messageId, job.Id, context.CancellationToken);

        _logger.LogInformation("Compile completed for job {JobId} -> {Path}", job.Id, resultPath);
        await _unitOfWork.SaveChangesAsync(context.CancellationToken);
    }

    private async Task RecordInboxAsync(Guid messageId, Guid jobId, CancellationToken cancellationToken)
    {
        await _inbox.AddAsync(new InboxProcessedDto
        {
            MessageId = messageId,
            ProcessedAt = DateTime.UtcNow,
            JobId = jobId
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string value) =>
        value.Length <= MaxErrorLength ? value : value[..MaxErrorLength];
}
