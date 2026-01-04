using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Domain.Common;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.Common.Services;

public sealed partial class PromptRegistry : IPromptRegistry
{
    private readonly string _defaultCvLatex;
    private readonly ILogger<PromptRegistry> _logger;

    private readonly Dictionary<PromptType, string> _templates = new()
    {
        [PromptType.CoverLetter] = @"
Write a compelling, professional cover letter based on the following information.

### GUIDELINES:
- Write in a professional yet engaging tone
- Highlight relevant experience and skills that match the job requirements
- Show enthusiasm for the role and company
- Keep it concise (3-4 paragraphs)
- Include a strong opening that grabs attention
- Connect the candidate's achievements to the job requirements
- End with a clear call to action
- Do NOT include placeholder text like [Company Name] - if unknown, write generically
- Do NOT make up information not present in the CV
- Make it look human-written and avoid AI-detection triggers

JOB DESCRIPTION:
{JobDescription}

CANDIDATE'S CV:
{CvText}

Write the cover letter now:",

        [PromptType.CvCustomization] = """
Generate a tailored CV in LaTeX that compiles on Overleaf using pdfLaTeX.
Output ONLY raw LaTeX (no JSON, no code fences, no quotes).
Do NOT escape backslashes or newlines (no \\documentclass, no \n).
Use \documentclass{{article}} only; no external .cls or custom files.

### CRITICAL LATEX RULES:
1. **ESCAPE SPECIAL CHARACTERS**: You MUST escape reserved LaTeX characters if they appear in text (e.g., #, $, %, &, _).
   - Write "C#" as "C\#" (NOT "C#")
   - Write ".NET" as ".NET" (Safe)
   - Write "C++" as "C++" (Safe)
   - Write "%" as "\%"
   - Write "&" as "\&"
   - Write "_" as "\_"
2. **NO UNICODE/NON-ASCII CHARACTERS**: Use ONLY ASCII characters (a-z, A-Z, 0-9). 
   - Convert Arabic/Chinese/Cyrillic to Latin transliteration
   - Example: "حالا" → "Hala", "北京" → "Beijing", "Москва" → "Moscow"
   - Use English equivalents for company/location names when possible
3. **NO MARKDOWN**: Do not use Markdown styling like **bold** or *italic*. Use \textbf{} and \textit{}.
4. **NO WEB LINKS**: Wrap all URLs in \url{...}.

### CUSTOMIZATION INSTRUCTIONS:
1. **KEYWORD INJECTION**: Scan the Job Description for technical skills and weave them into the CV.
2. **ACTIVE REPHRASING**: Rephrase bullet points to show how past experience solves the needs of the Job Description.
3. **OBJECTIVE REWRITE**: Pitch the candidate specifically for the role.
4. **PRIORITIZATION**: Move the most relevant projects or experiences to the top.
5. **SKILLS CATEGORIZATION**: Group skills logically into 3-5 professional categories (e.g., Languages, Technologies, Cloud & DevOps).

JOB DESCRIPTION:
{JobDescription}

CANDIDATE INFORMATION:
{CvText}

{ConfirmedSkills}

LATEX STRUCTURE TEMPLATE:
{LatexTemplate}

Write the customized raw LaTeX source now:
""",

        [PromptType.MatchAnalysis] = @"
Analyze the compatibility between the following CV and Job Description.
Return the result ONLY as a JSON object with these fields:
- matchScore: (integer, 0-100)
- matchingKeywords: (array of strings, key technical/soft skills present in both)
- missingKeywords: (array of strings, important skills mentioned in the job but missing in the CV)
- analysisSummary: (short 2-sentence summary of the fit)

JOB DESCRIPTION:
{JobDescription}

CV CONTENT:
{CvText}

Output only valid JSON:"
    };

    // Track required variables for each prompt type
    private readonly Dictionary<PromptType, HashSet<string>> _requiredVariables = new()
    {
        [PromptType.CoverLetter] = new() { "JobDescription", "CvText" },
        [PromptType.CvCustomization] = new() { "JobDescription", "CvText", "ConfirmedSkills", "LatexTemplate" },
        [PromptType.MatchAnalysis] = new() { "JobDescription", "CvText" }
    };

    public PromptRegistry(ILogger<PromptRegistry> logger)
    {
        _logger = logger;

        // Safely load default CV template with error handling
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "DefaultCv.tex");
        try
        {
            _defaultCvLatex = File.ReadAllText(templatePath);
            _logger.LogDebug("Loaded default CV template from {Path}", templatePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load default CV template from {Path}", templatePath);
            throw new InvalidOperationException($"Required template file not found: {templatePath}", ex);
        }
    }

    public Result<string> GetPrompt(PromptType type, Dictionary<string, string> variables)
    {
        if (!_templates.TryGetValue(type, out var template))
            return Result<string>.Failure($"Template for {type} not found.", ResultType.NotFound);

        // Inject default LaTeX if not provided for CV customization
        if (type == PromptType.CvCustomization && !variables.ContainsKey("LatexTemplate"))
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["PromptType"] = type.ToString(),
                ["LatexTemplateInjected"] = true
            });
            _logger.LogDebug("Injecting default LaTeX template for CV customization");
            variables["LatexTemplate"] = _defaultCvLatex;
        }

        var result = variables.Aggregate(template, (current, variable) =>
            current.Replace("{" + variable.Key + "}", variable.Value));

        return Result<string>.Success(result);
    }

    public Result<string> GetRawTemplate(PromptType type)
    {
        if (!_templates.TryGetValue(type, out var template))
            return Result<string>.Failure($"Template for {type} not found.", ResultType.NotFound);

        // For CV customization, inject the default LaTeX template placeholder
        if (type == PromptType.CvCustomization)
        {
            template = template.Replace("{LatexTemplate}", _defaultCvLatex);
        }

        return Result<string>.Success(template);
    }

    public Result<IReadOnlyCollection<string>> GetRequiredVariables(PromptType type)
    {
        if (!_requiredVariables.TryGetValue(type, out var variables))
            return Result<IReadOnlyCollection<string>>.Failure($"Variables for {type} not found.", ResultType.NotFound);

        return Result<IReadOnlyCollection<string>>.Success(variables);
    }
}
