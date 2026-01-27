namespace CoverLetter.Application.UseCases.AnswerTextareaQuestion;

/// <summary>
/// Result of generating an answer to a textarea question.
/// </summary>
public sealed record AnswerQuestionResult(
    string Answer
);
