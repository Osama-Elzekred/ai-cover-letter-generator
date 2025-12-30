using CoverLetter.Domain.Common;
using MediatR;

namespace CoverLetter.Application.UseCases.MatchCv;

public sealed record MatchCvCommand(
    string CvId,
    string JobDescription,
    string? IdempotencyKey = null
) : IRequest<Result<MatchCvResult>>;

public sealed record MatchCvResult(
    int MatchScore,
    List<string> MatchingKeywords,
    List<string> MissingKeywords,
    string AnalysisSummary,
    string Model,
    int TotalTokens
);
