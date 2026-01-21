using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.UseCases.GetCv;

/// <summary>
/// Handler for GetCvQuery.
/// Queries use IQueryContext directly with LINQ.
/// Handler manages Result<T> wrapping.
/// </summary>
public sealed class GetCvQueryHandler(
    IQueryContext queryContext,
    ILogger<GetCvQueryHandler> logger)
    : IRequestHandler<GetCvQuery, Result<GetCvResult>>
{
  public async Task<Result<GetCvResult>> Handle(GetCvQuery request, CancellationToken cancellationToken)
  {
    try
    {
      using var scope = logger.BeginScope(new Dictionary<string, object>
      {
        ["CvId"] = request.CvId
      });

      var cv = await queryContext.Cvs
          .Where(x => x.Id == request.CvId)
          .Select(x => new GetCvResult(
              Id: x.Id,
              FileName: x.FileName,
              CreatedAt: x.CreatedAt
          ))
          .FirstOrDefaultAsync(cancellationToken);

      if (cv is null)
      {
        logger.LogWarning("CV not found: {CvId}", request.CvId);
        return Result<GetCvResult>.NotFound($"CV not found: {request.CvId}");
      }

      logger.LogInformation("CV retrieved successfully: {CvId}", request.CvId);
      return Result.Success(cv);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed to retrieve CV");
      return Result.Failure<GetCvResult>($"Failed to retrieve CV: {ex.Message}");
    }
  }
}
