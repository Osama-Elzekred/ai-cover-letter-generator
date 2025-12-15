namespace CoverLetter.Application.UseCases.GenerateCoverLetter;

/// <summary>
/// Defines how custom prompt instructions should be combined with the default prompt.
/// </summary>
public enum PromptMode
{
  /// <summary>
  /// Append custom instructions to the default prompt template.
  /// Default behavior - adds extra guidance while keeping standard instructions.
  /// </summary>
  Append = 0,

  /// <summary>
  /// Replace the entire default prompt template with custom instructions.
  /// Advanced use - gives full control but requires complete prompt engineering.
  /// </summary>
  Override = 1
}
