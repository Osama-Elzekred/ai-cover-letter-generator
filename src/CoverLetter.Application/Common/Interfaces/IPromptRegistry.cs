using System.Collections.Generic;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Enums;

namespace CoverLetter.Application.Common.Interfaces;

public interface IPromptRegistry
{
    /// <summary>
    /// Gets a prompt template with variables replaced.
    /// Returns Result.Success with the filled prompt, or Result.Failure if template not found.
    /// </summary>
    Result<string> GetPrompt(PromptType type, Dictionary<string, string> variables);

    /// <summary>
    /// Gets the raw template with variable placeholders intact (e.g., {JobDescription}).
    /// Useful for preview/transparency endpoints.
    /// </summary>
    Result<string> GetRawTemplate(PromptType type);

    /// <summary>
    /// Returns the list of required variable names for a given prompt type.
    /// </summary>
    Result<IReadOnlyCollection<string>> GetRequiredVariables(PromptType type);
}
