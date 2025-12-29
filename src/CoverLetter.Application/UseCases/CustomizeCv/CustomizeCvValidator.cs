using FluentValidation;

namespace CoverLetter.Application.UseCases.CustomizeCv;

public sealed class CustomizeCvValidator : AbstractValidator<CustomizeCvCommand>
{
    public CustomizeCvValidator()
    {
        RuleFor(x => x.CvId)
            .NotEmpty().WithMessage("CV ID is required.");

        RuleFor(x => x.JobDescription)
            .NotEmpty().WithMessage("Job description is required.")
            .MinimumLength(50).WithMessage("Job description is too short (min 50 chars).");
    }
}
