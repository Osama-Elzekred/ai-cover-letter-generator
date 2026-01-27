using FluentValidation;

namespace CoverLetter.Application.UseCases.AnswerTextareaQuestion;

/// <summary>
/// Validator for AnswerQuestionCommand.
/// </summary>
public sealed class AnswerQuestionValidator : AbstractValidator<AnswerQuestionCommand>
{
  public AnswerQuestionValidator()
  {
    RuleFor(x => x.CvId)
        .NotEmpty()
        .WithMessage("CV ID is required");

    RuleFor(x => x.FieldLabel)
        .NotEmpty()
        .WithMessage("Field label is required")
        .MaximumLength(200)
        .WithMessage("Field label must not exceed 200 characters");

    RuleFor(x => x.UserQuestion)
        .NotEmpty()
        .WithMessage("Question is required")
        .MaximumLength(2000)
        .WithMessage("Question must not exceed 2000 characters");

    RuleFor(x => x.JobDescription)
        .MaximumLength(50000)
        .WithMessage("Job description must not exceed 50000 characters")
        .When(x => x.JobDescription != null);

    RuleFor(x => x.CustomPromptTemplate)
        .MaximumLength(10000)
        .WithMessage("Custom prompt template must not exceed 10000 characters")
        .When(x => x.CustomPromptTemplate != null);
  }
}
