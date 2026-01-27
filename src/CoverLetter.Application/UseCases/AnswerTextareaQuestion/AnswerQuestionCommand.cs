using CoverLetter.Application.Common.Behaviors;
using CoverLetter.Domain.Common;
using MediatR;

namespace CoverLetter.Application.UseCases.AnswerTextareaQuestion;

/// <summary>
/// Command to generate an answer to a textarea question using CV info.
/// User answers job application questions (e.g., "Why are you interested?")
/// using their stored CV and optional job context.
/// Supports idempotency for safe retries.
/// </summary>
public sealed record AnswerQuestionCommand(
    string CvId,
    string FieldLabel,
    string UserQuestion,
    string? JobTitle = null,
    string? CompanyName = null,
    string? JobDescription = null,
    string? CustomPromptTemplate = null,
    string? IdempotencyKey = null
) : IRequest<Result<AnswerQuestionResult>>, IIdempotentRequest;
