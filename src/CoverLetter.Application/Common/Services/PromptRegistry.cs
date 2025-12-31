using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using CoverLetter.Application.Common.Interfaces;

namespace CoverLetter.Application.Common.Services;

public sealed class PromptRegistry : IPromptRegistry
{
    private static readonly string DefaultCvLatex = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Templates", "DefaultCv.tex"));

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

        [PromptType.CvCustomization] = @"
Generate a tailored CV in LaTeX that compiles on Overleaf using pdfLaTeX.
Output ONLY raw LaTeX (no JSON, no code fences, no quotes).
Do NOT escape backslashes or newlines (no \\documentclass, no \n).
Use \documentclass{{article}} only; no external .cls or custom files.

### CRITICAL CUSTOMIZATION RULES:
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

Write the customized raw LaTeX source now:",

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

    public string GetPrompt(PromptType type, Dictionary<string, string> variables)
    {
        if (!_templates.TryGetValue(type, out var template))
            throw new ArgumentException($"Template for {type} not found.");

        // Inject default LaTeX if not provided for CV customization
        if (type == PromptType.CvCustomization && !variables.ContainsKey("LatexTemplate"))
        {
            variables["LatexTemplate"] = DefaultCvLatex;
        }

        return variables.Aggregate(template, (current, variable) => 
            current.Replace("{" + variable.Key + "}", variable.Value));
    }
}
