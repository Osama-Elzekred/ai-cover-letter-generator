using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Common.Services;
using CoverLetter.Application.Repositories;
using CoverLetter.Application.UseCases.Compile;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CoverLetter.Application.Tests.UseCases.Compile;

/// <summary>
/// Unit tests for EnqueueCompileHandler (and the underlying CompileJobEnqueuer logic
/// observable through it: transactional job + outbox creation and DB-backed idempotency).
/// </summary>
public class EnqueueCompileHandlerTests
{
  private readonly ICompileJobEnqueuer _enqueuer;
  private readonly IUserContext _userContext;
  private readonly ILogger<EnqueueCompileHandler> _logger;
  private readonly EnqueueCompileHandler _handler;

  public EnqueueCompileHandlerTests()
  {
    _enqueuer = Substitute.For<ICompileJobEnqueuer>();
    _userContext = Substitute.For<IUserContext>();
    _logger = Substitute.For<ILogger<EnqueueCompileHandler>>();

    _userContext.UserId.Returns((string?)"user-123");

    _handler = new EnqueueCompileHandler(_enqueuer, _userContext, _logger);
  }

  [Fact]
  public async Task Handle_ValidRequest_EnqueuesAndReturnsJobId()
  {
    // Arrange
    var jobId = Guid.NewGuid();
    _enqueuer.EnqueueAsync(
        "user-123",
        "key-1",
        Arg.Any<string>(),
        Arg.Any<CancellationToken>())
      .Returns(Result.Success(new CompileJobEnqueueResult(jobId)));

    var command = new EnqueueCompileCommand(LatexSource: "\\documentclass{article}", IdempotencyKey: "key-1");

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.JobId.Should().Be(jobId);
    await _enqueuer.Received(1).EnqueueAsync("user-123", "key-1", "\\documentclass{article}", Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_EnqueuerFails_PropagatesFailure()
  {
    // Arrange
    _enqueuer.EnqueueAsync(
        Arg.Any<string?>(),
        Arg.Any<string?>(),
        Arg.Any<string>(),
        Arg.Any<CancellationToken>())
      .Returns(Result<CompileJobEnqueueResult>.Failure("DB unavailable"));

    var command = new EnqueueCompileCommand(LatexSource: "\\documentclass{article}");

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Should().Contain("DB unavailable");
  }
}

/// <summary>
/// Unit tests for CompileJobEnqueuer covering transactional creation and
/// DB-backed idempotency (replay + unique-constraint race).
/// </summary>
public class CompileJobEnqueuerTests
{
  private readonly ICompileJobRepository _compileJobRepository;
  private readonly IOutboxMessageRepository _outboxRepository;
  private readonly IUnitOfWork _unitOfWork;
  private readonly ILogger<CompileJobEnqueuer> _logger;
  private readonly CompileJobEnqueuer _enqueuer;

  public CompileJobEnqueuerTests()
  {
    _compileJobRepository = Substitute.For<ICompileJobRepository>();
    _outboxRepository = Substitute.For<IOutboxMessageRepository>();
    _unitOfWork = Substitute.For<IUnitOfWork>();
    _logger = Substitute.For<ILogger<CompileJobEnqueuer>>();

    _enqueuer = new CompileJobEnqueuer(_compileJobRepository, _outboxRepository, _unitOfWork, _logger);
  }

  [Fact]
  public async Task EnqueueAsync_ValidInput_CreatesJobAndOutboxAndSaves()
  {
    // Arrange
    var jobId = Guid.NewGuid();
    _compileJobRepository.AddAsync(Arg.Any<CompileJobDto>(), Arg.Any<CancellationToken>())
      .Returns(new CompileJobDto { Id = jobId, Status = CompileJobStatus.Pending });

    // Act
    var result = await _enqueuer.EnqueueAsync("user-1", "key-1", "\\documentclass{article}", CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.JobId.Should().Be(jobId);
    await _compileJobRepository.Received(1).AddAsync(Arg.Is<CompileJobDto>(d => d.UserId == "user-1" && d.IdempotencyKey == "key-1"), Arg.Any<CancellationToken>());
    await _outboxRepository.Received(1).AddAsync(Arg.Any<OutboxMessageDto>(), Arg.Any<CancellationToken>());
    await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task EnqueueAsync_EmptyLatex_ReturnsValidationError()
  {
    // Act
    var result = await _enqueuer.EnqueueAsync("user-1", null, "", CancellationToken.None);

    // Assert
    result.IsFailure.Should().BeTrue();
    result.Type.Should().Be(ResultType.ValidationError);
    await _compileJobRepository.DidNotReceive().AddAsync(Arg.Any<CompileJobDto>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task EnqueueAsync_ExistingIdempotencyKey_ReturnsExistingJob()
  {
    // Arrange
    var existingJobId = Guid.NewGuid();
    _compileJobRepository.FindByIdempotencyKeyAsync("user-1", "key-1", Arg.Any<CancellationToken>())
      .Returns(new CompileJobDto { Id = existingJobId, Status = CompileJobStatus.Pending });

    // Act
    var result = await _enqueuer.EnqueueAsync("user-1", "key-1", "\\documentclass{article}", CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.JobId.Should().Be(existingJobId);
    await _compileJobRepository.DidNotReceive().AddAsync(Arg.Any<CompileJobDto>(), Arg.Any<CancellationToken>());
    await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task EnqueueAsync_NoIdempotencyKey_SkipsReplayCheck()
  {
    // Arrange
    _compileJobRepository.AddAsync(Arg.Any<CompileJobDto>(), Arg.Any<CancellationToken>())
      .Returns(new CompileJobDto { Id = Guid.NewGuid(), Status = CompileJobStatus.Pending });

    // Act
    var result = await _enqueuer.EnqueueAsync("user-1", null, "\\documentclass{article}", CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    await _compileJobRepository.DidNotReceive().FindByIdempotencyKeyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task EnqueueAsync_SaveThrowsUniqueConstraint_RaceResolvedToExistingJob()
  {
    // Arrange
    _compileJobRepository.AddAsync(Arg.Any<CompileJobDto>(), Arg.Any<CancellationToken>())
      .Returns(new CompileJobDto { Id = Guid.NewGuid(), Status = CompileJobStatus.Pending });

    var winnerId = Guid.NewGuid();
    _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
      .Throws(new DbUpdateException("duplicate key"));
    _compileJobRepository.FindByIdempotencyKeyAsync("user-1", "key-1", Arg.Any<CancellationToken>())
      .Returns(new CompileJobDto { Id = winnerId, Status = CompileJobStatus.Pending });

    // Act
    var result = await _enqueuer.EnqueueAsync("user-1", "key-1", "\\documentclass{article}", CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.JobId.Should().Be(winnerId);
  }
}
