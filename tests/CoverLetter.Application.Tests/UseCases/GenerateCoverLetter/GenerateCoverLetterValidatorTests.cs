using CoverLetter.Application.UseCases.GenerateCoverLetter;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace CoverLetter.Application.Tests.UseCases.GenerateCoverLetter;

/// <summary>
/// Unit tests for GenerateCoverLetterValidator.
/// </summary>
public class GenerateCoverLetterValidatorTests
{
  private readonly GenerateCoverLetterValidator _validator = new();

  [Fact]
  public void Validate_ValidCommand_ShouldNotHaveErrors()
  {
    // Arrange
    var command = new GenerateCoverLetterCommand(
        JobDescription: "Valid job description",
        CvText: "Valid CV text"
    );

    // Act
    var result = _validator.TestValidate(command);

    // Assert
    result.ShouldNotHaveAnyValidationErrors();
  }

  [Fact]
  public void Validate_EmptyJobDescription_ShouldHaveError()
  {
    // Arrange
    var command = new GenerateCoverLetterCommand(
        JobDescription: "",
        CvText: "Valid CV"
    );

    // Act
    var result = _validator.TestValidate(command);

    // Assert
    result.ShouldHaveValidationErrorFor(x => x.JobDescription)
        .WithErrorMessage("Job description is required.");
  }

  [Fact]
  public void Validate_EmptyCvText_ShouldHaveError()
  {
    // Arrange
    var command = new GenerateCoverLetterCommand(
        JobDescription: "Valid job",
        CvText: ""
    );

    // Act
    var result = _validator.TestValidate(command);

    // Assert
    result.ShouldHaveValidationErrorFor(x => x.CvText)
        .WithErrorMessage("CV text is required.");
  }

  [Fact]
  public void Validate_JobDescriptionTooLong_ShouldHaveError()
  {
    // Arrange
    var command = new GenerateCoverLetterCommand(
        JobDescription: new string('x', 50001),
        CvText: "Valid CV"
    );

    // Act
    var result = _validator.TestValidate(command);

    // Assert
    result.ShouldHaveValidationErrorFor(x => x.JobDescription)
        .WithErrorMessage("Job description exceeds maximum length of 50,000 characters.");
  }

  [Fact]
  public void Validate_CvTextTooLong_ShouldHaveError()
  {
    // Arrange
    var command = new GenerateCoverLetterCommand(
        JobDescription: "Valid job",
        CvText: new string('x', 50001)
    );

    // Act
    var result = _validator.TestValidate(command);

    // Assert
    result.ShouldHaveValidationErrorFor(x => x.CvText)
        .WithErrorMessage("CV text exceeds maximum length of 50,000 characters.");
  }

  [Fact]
  public void Validate_NullCustomTemplate_ShouldNotHaveError()
  {
    // Arrange
    var command = new GenerateCoverLetterCommand(
        JobDescription: "Valid job",
        CvText: "Valid CV",
        CustomPromptTemplate: null
    );

    // Act
    var result = _validator.TestValidate(command);

    // Assert
    result.ShouldNotHaveValidationErrorFor(x => x.CustomPromptTemplate);
  }

  [Fact]
  public void Validate_CustomTemplateTooLong_ShouldHaveError()
  {
    // Arrange
    var command = new GenerateCoverLetterCommand(
        JobDescription: "Valid job",
        CvText: "Valid CV",
        CustomPromptTemplate: new string('x', 10001)
    );

    // Act
    var result = _validator.TestValidate(command);

    // Assert
    result.ShouldHaveValidationErrorFor(x => x.CustomPromptTemplate);
  }
}
