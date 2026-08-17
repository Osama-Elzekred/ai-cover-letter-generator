using CoverLetter.Application.Repositories;
using CoverLetter.Application.UseCases.Compile;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Reflection;

namespace CoverLetter.Application.Tests.UseCases.Compile;

/// <summary>
/// Unit tests for GetCompileStatusHandler.
/// Uses NSubstitute to mock ICompileJobRepository.GetByIdAsync.
/// </summary>
public class GetCompileStatusHandlerTests
{
  private readonly ICompileJobRepository _compileJobRepository;
  private readonly ILogger<GetCompileStatusHandler> _logger;
  private readonly GetCompileStatusHandler _handler;

  public GetCompileStatusHandlerTests()
  {
    _compileJobRepository = Substitute.For<ICompileJobRepository>();
    _logger = Substitute.For<ILogger<GetCompileStatusHandler>>();
    _handler = new GetCompileStatusHandler(_compileJobRepository, _logger);
  }

  [Fact]
  public async Task Handle_JobNotFound_ReturnsNotFound()
  {
    // Arrange
    _compileJobRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
      .Returns((CompileJobDto?)null);

    // Act
    var result = await _handler.Handle(new GetCompileStatusQuery(Guid.NewGuid()), CancellationToken.None);

    // Assert
    result.IsFailure.Should().BeTrue();
    result.Type.Should().Be(ResultType.NotFound);
  }

  [Fact]
  public async Task Handle_PendingJob_ReturnsStatusWithoutDownloadUrl()
  {
    // Arrange
    var jobId = Guid.NewGuid();
    _compileJobRepository.GetByIdAsync(jobId, Arg.Any<CancellationToken>())
      .Returns(CreateJobDto(jobId, CompileJobStatus.Pending));

    // Act
    var result = await _handler.Handle(new GetCompileStatusQuery(jobId), CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.JobId.Should().Be(jobId);
    result.Value.Status.Should().Be("Pending");
    result.Value.DownloadUrl.Should().BeNull();
  }

  [Fact]
  public async Task Handle_CompletedJob_ReturnsStatusWithDownloadUrl()
  {
    // Arrange
    var jobId = Guid.NewGuid();
    _compileJobRepository.GetByIdAsync(jobId, Arg.Any<CancellationToken>())
      .Returns(CreateJobDto(jobId, CompileJobStatus.Completed, resultPath: "/data/pdfs/abc.pdf"));

    // Act
    var result = await _handler.Handle(new GetCompileStatusQuery(jobId), CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Status.Should().Be("Completed");
    result.Value.DownloadUrl.Should().Be($"/api/v1/cv/compile/result/{jobId}");
  }

  [Fact]
  public async Task Handle_FailedJob_ReturnsStatusWithError()
  {
    // Arrange
    var jobId = Guid.NewGuid();
    _compileJobRepository.GetByIdAsync(jobId, Arg.Any<CancellationToken>())
      .Returns(CreateJobDto(jobId, CompileJobStatus.Failed, error: "LaTeX syntax error"));

    // Act
    var result = await _handler.Handle(new GetCompileStatusQuery(jobId), CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Status.Should().Be("Failed");
    result.Value.Error.Should().Be("LaTeX syntax error");
    result.Value.DownloadUrl.Should().BeNull();
  }

  private static CompileJobDto CreateJobDto(
      Guid id,
      CompileJobStatus status,
      string? resultPath = null,
      string? error = null) => new()
  {
    Id = id,
    Status = status,
    UserId = "user-1",
    IdempotencyKey = null,
    ResultPath = resultPath,
    Error = error,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
  };
}
