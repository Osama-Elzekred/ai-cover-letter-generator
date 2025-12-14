using FluentValidation;

namespace CoverLetter.Application.UseCases.GenerateCoverLetter;

/// <summary>
/// Validator for GenerateCoverLetterCommand.
/// </summary>
public sealed class GenerateCoverLetterValidator : AbstractValidator<GenerateCoverLetterCommand>
{
  public GenerateCoverLetterValidator()
  {
    RuleFor(x => x.JobDescription)
        .NotEmpty()
        .WithMessage("Job description is required.")
        .MaximumLength(50000)
        .WithMessage("Job description exceeds maximum length of 50,000 characters.");

    // Either CvId OR CvText must be provided
    RuleFor(x => x)
        .Must(x => !string.IsNullOrWhiteSpace(x.CvId) || !string.IsNullOrWhiteSpace(x.CvText))
        .WithMessage("Either CvId or CvText must be provided.");

    // CvText validation (when provided)
    RuleFor(x => x.CvText)
        .MaximumLength(50000)
        .WithMessage("CV text exceeds maximum length of 50,000 characters.")
        .When(x => !string.IsNullOrWhiteSpace(x.CvText));

    RuleFor(x => x.CustomPromptTemplate)
        .MaximumLength(10000)
        .WithMessage("Custom prompt template exceeds maximum length of 10,000 characters.")
        .When(x => x.CustomPromptTemplate is not null);
  }
}
