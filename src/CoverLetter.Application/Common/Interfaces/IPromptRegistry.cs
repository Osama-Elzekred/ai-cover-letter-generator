using System.Collections.Generic;

namespace CoverLetter.Application.Common.Interfaces;

public enum PromptType
{
    CoverLetter,
    CvCustomization,
    MatchAnalysis
}

public interface IPromptRegistry
{
    string GetPrompt(PromptType type, Dictionary<string, string> variables);
}
