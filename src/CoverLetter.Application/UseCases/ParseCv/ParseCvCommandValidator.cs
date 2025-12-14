using FluentValidation;
using CoverLetter.Domain.Entities;

namespace CoverLetter.Application.UseCases.ParseCv;

/// <summary>
/// Validator for CV parsing requests.
/// Ensures file is valid before processing.
/// </summary>
public sealed class ParseCvCommandValidator : AbstractValidator<ParseCvCommand>
{
  private const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
  private const int MinFileSizeBytes = 100; // Minimum 100 bytes

  private static readonly CvFormat[] SupportedFormats =
  [
      CvFormat.Pdf,
      CvFormat.PlainText,
      CvFormat.LaTeX
  ];

  public ParseCvCommandValidator()
  {
    RuleFor(x => x.FileName)
        .NotEmpty()
        .WithMessage("File name is required.")
        .MaximumLength(255)
        .WithMessage("File name is too long (max 255 characters).");

    RuleFor(x => x.FileContent)
        .NotNull()
        .WithMessage("File content is required.")
        .Must(content => content.Length >= MinFileSizeBytes)
        .WithMessage($"File is too small. Minimum size is {MinFileSizeBytes} bytes.")
        .Must(content => content.Length <= MaxFileSizeBytes)
        .WithMessage($"File is too large. Maximum size is {MaxFileSizeBytes / (1024 * 1024)} MB.");

    RuleFor(x => x.Format)
        .Must(format => SupportedFormats.Contains(format))
        .WithMessage($"Unsupported CV format. Allowed formats: {string.Join(", ", SupportedFormats)}.");

    // File extension validation (optional but recommended)
    RuleFor(x => x)
        .Must(cmd => ValidateFileExtension(cmd.FileName, cmd.Format))
        .WithMessage(cmd => $"File extension does not match format. Expected extension for {cmd.Format}.");
  }

  private static bool ValidateFileExtension(string fileName, CvFormat format)
  {
    var extension = Path.GetExtension(fileName).ToLowerInvariant();

    return format switch
    {
      CvFormat.Pdf => extension == ".pdf",
      CvFormat.LaTeX => extension is ".tex" or ".latex",
      CvFormat.PlainText => extension is ".txt" or ".text",
      _ => false
    };
  }
}
