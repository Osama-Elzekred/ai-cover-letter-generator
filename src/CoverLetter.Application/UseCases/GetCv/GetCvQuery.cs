using CoverLetter.Domain.Common;
using MediatR;

namespace CoverLetter.Application.UseCases.GetCv;

/// <summary>
/// Query to retrieve a previously parsed CV by ID
/// </summary>
public sealed record GetCvQuery(Guid CvId) : IRequest<Result<GetCvResult>>;

/// <summary>
/// Response: CV metadata only (full content can be requested separately if needed)
/// </summary>
public sealed record GetCvResult(
    Guid Id,
    string FileName,
    DateTime CreatedAt
);
