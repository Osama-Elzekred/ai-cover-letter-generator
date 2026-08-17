using FluentValidation;

namespace CoverLetter.Application.UseCases.Compile;

/// <summary>
/// Validator for <see cref="EnqueueCompileCommand"/>.
/// Enforces a maximum LaTeX payload size to prevent DoS via huge compilations.
/// </summary>
public sealed class EnqueueCompileValidator : AbstractValidator<EnqueueCompileCommand>
{
    /// <summary>
    /// Maximum accepted LaTeX source length (approx 100k chars).
    /// </summary>
    public const int MaxLatexLength = 100_000;

    public EnqueueCompileValidator()
    {
        RuleFor(x => x.LatexSource)
            .NotEmpty().WithMessage("LaTeX source cannot be empty.")
            .MaximumLength(MaxLatexLength).WithMessage(
                $"LaTeX source exceeds maximum length of {MaxLatexLength:N0} characters.");
    }
}
