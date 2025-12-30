using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.UseCases.GenerateCoverLetter;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.UseCases.CustomizeCv;

/// <summary>
/// Handler for CustomizeCvCommand.
/// Uses AI to map CV text into a professional LaTeX template tailored to a job description.
/// </summary>
public sealed class CustomizeCvHandler(
    ILlmService llmService,
    ILatexCompilerService latexCompilerService,
    IMemoryCache cache,
    IUserContext userContext,
    ILogger<CustomizeCvHandler> logger)
    : IRequestHandler<CustomizeCvCommand, Result<CustomizeCvResult>>
{
    private const string DefaultLatexTemplate = @"
\documentclass[10pt, letterpaper]{article}
\usepackage[left=0.4in,top=0.4in,right=0.4in,bottom=0.4in]{geometry}
\usepackage[utf8]{inputenc}
\usepackage[hidelinks]{hyperref}
\usepackage{enumitem}
\usepackage{titlesec}
\usepackage{xcolor}
\usepackage{amssymb}

\pagestyle{empty}

% Section formatting
\titleformat{\section}{\large\bfseries\uppercase}{}{0pt}{}[\titlerule]
\titlespacing{\section}{0pt}{8pt}{4pt}

% Custom commands from user's CV
\newcommand{\tab}[1]{\hspace{.2667\textwidth}\rlap{#1}} 
\newcommand{\itab}[1]{\hspace{0em}\rlap{#1}}

% emulating the 'resume' header style
\newcommand{\name}[1]{{\centerline{\Huge\bfseries #1}}\vspace{0.7em}}
\newcommand{\address}[1]{{\centerline{#1}}\vspace{0.2em}}

\begin{document}

\name{Your Name}
\address{$\diamond$ Your Phone \quad $\diamond$ Your Country}
\address{$\diamond$ \href{mailto:your@email.com}{your@email.com} \quad $\diamond$ \href{https://linkedin.com/in/your-link}{linkedin.com/in/your-link} \quad $\diamond$ \href{https://github.com/your-github}{github.com/your-github}}

\section{Objective}
[A concise professional summary tailored to the job description]

\section{Education}
{\bf Degree Name}, University Name \hfill {Dates} \\
Overall Grade/GPA: X.XX

\section{Skills}
\textbf{Languages:} C#, Python, JavaScript, TypeScript \\
\textbf{Technologies:} .NET, Docker, Kubernetes, Azure, AWS \\
\textbf{Tools \& Concepts:} Git, CI/CD, Agile, Microservices, REST APIs

\section{Experience}
\textbf{Job Title} \hfill {Dates} \\
\textit{Company Name} \hfill {Location}
\begin{itemize}[noitemsep, topsep=0pt, leftmargin=0.15in]
    \item Achievement or responsibility 1
    \item Achievement or responsibility 2
\end{itemize}

\section{Projects}
\begin{itemize}[noitemsep, topsep=0pt, leftmargin=0.15in]
    \item \textbf{Project Name} --- Description and tech stack.
\end{itemize}

\section{Extra-Curricular Activities}
\begin{itemize}[noitemsep, topsep=0pt, leftmargin=0.15in]
    \item Activity or organization
    \item Another activity
\end{itemize}

\end{document}
";


    public async Task<Result<CustomizeCvResult>> Handle(
        CustomizeCvCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Resolve CV text
            var cacheKey = $"cv:{request.CvId}";
            if (!cache.TryGetValue<CvDocument>(cacheKey, out var cvDocument) || cvDocument is null)
            {
                logger.LogWarning("CV not found in cache: {CvId}", request.CvId);
                return Result<CustomizeCvResult>.Failure(
                    $"CV with ID '{request.CvId}' not found. The CV may have expired or the ID is invalid.",
                    ResultType.NotFound);
            }

            // 2. Build the base prompt
            string basePrompt = $@"
Generate a tailored CV in LaTeX that compiles on Overleaf using pdfLaTeX.
Output ONLY raw LaTeX (no JSON, no code fences, no quotes).
Do NOT escape backslashes or newlines (no \\documentclass, no \n).
Use \documentclass{{article}} only; no external .cls or custom files.
Define any custom commands inside the same file.
Ensure no \item outside list environments.
Escape special characters in text (# $ % & _ {{ }} ~ ^).
Output must start with \documentclass and end with \end{{document}}.

### CRITICAL CUSTOMIZATION RULES:
1. **KEYWORD INJECTION**: Scan the Job Description for technical skills (e.g., .Net, C#, Python, Rust, PostgreSQL) and soft skills. YOU MUST weave these keywords into the CV.
2. **ACTIVE REPHRASING**: Do NOT just copy the candidate's existing text. Rephrase bullet points to show how their past experience directly solves the needs of the Job Description.
3. **OBJECTIVE/SUMMARY REWRITE**: Completely rewrite the 'Objective' section to pitch the candidate specifically for the role mentioned in the Job Description.
4. **PRIORITIZATION**: Move the candidate's most relevant projects or experiences to the top of their respective sections.
5. **MATCHING TERMINOLOGY**: If the job asks for 'Native Apps' and the candidate has 'Mobile development', change the CV text to use the job's terminology where accurate.
6. **SKILLS CATEGORIZATION**: Group skills logically into 3-5 professional categories. Standard headers include `Languages`, `Technologies`, `Cloud & DevOps`, `Databases`, or `Tools & Concepts`. Keep category names concise (1-3 words).
";

            // Add selected keywords if any
            if (request.SelectedKeywords != null && request.SelectedKeywords.Any())
            {
                var keywordsList = string.Join(", ", request.SelectedKeywords);
                basePrompt += $"\n7. **CONFIRMED SKILLS**: The user has specifically confirmed they possess the following skills: {keywordsList}. You MUST ensure these are clearly integrated into the CV.\n";
            }

            string finalPrompt;
            if (request.PromptMode == PromptMode.Override && !string.IsNullOrWhiteSpace(request.CustomPromptTemplate))
            {
                finalPrompt = $@"{basePrompt}
### USER CUSTOM INSTRUCTIONS (OVERRIDE):
{request.CustomPromptTemplate}

JOB DESCRIPTION:
{request.JobDescription}

CANDIDATE INFORMATION:
{cvDocument.ExtractedText}

LATEX STRUCTURE TEMPLATE:
{DefaultLatexTemplate}

Write the customized raw LaTeX source now:
";
            }
            else
            {
                var customInstructions = !string.IsNullOrWhiteSpace(request.CustomPromptTemplate) 
                    ? $"\n### ADDITIONAL USER INSTRUCTIONS:\n{request.CustomPromptTemplate}\n" 
                    : "";

                finalPrompt = $@"{basePrompt}{customInstructions}
JOB DESCRIPTION:
{request.JobDescription}

CANDIDATE INFORMATION:
{cvDocument.ExtractedText}

LATEX STRUCTURE TEMPLATE:
{DefaultLatexTemplate}

Write the customized raw LaTeX source now:
";
            }

            var userApiKey = userContext.GetUserApiKey();
            var llmOptions = new LlmGenerationOptions(
                SystemMessage: "You are a professional LaTeX CV expert. Respond ONLY with raw LaTeX code, no extra text, no markdown fences.",
                ApiKey: userApiKey
            );

            logger.LogInformation("Generating customized LaTeX for CV {CvId}", request.CvId);
            var llmResponse = await llmService.GenerateAsync(finalPrompt, llmOptions, cancellationToken);
            
            var latexSource = ExtractLatexFromResponse(llmResponse.Content);

            // 3. Compile LaTeX to PDF (unless LaTeX only requested)
            byte[]? pdfBytes = null;
            if (!request.ReturnLatexOnly)
            {
                logger.LogInformation("Compiling customized LaTeX to PDF for CV {CvId}", request.CvId);
                pdfBytes = await latexCompilerService.CompileToPdfAsync(latexSource, cancellationToken);
            }

            var result = new CustomizeCvResult(
                PdfContent: pdfBytes,
                LatexSource: request.ReturnLatexOnly ? latexSource : null,
                FileName: request.ReturnLatexOnly ? "customized_cv.tex" : "customized_cv.pdf",
                Model: llmResponse.Model,
                PromptTokens: llmResponse.PromptTokens,
                CompletionTokens: llmResponse.CompletionTokens,
                GeneratedAt: DateTime.UtcNow
            );

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to customize CV for {CvId}", request.CvId);
            return Result.Failure<CustomizeCvResult>($"Failed to customize CV: {ex.Message}");
        }
    }

    private string ExtractLatexFromResponse(string content)
    {
        content = content.Trim();

        // 1. Remove Markdown code fences
        if (content.StartsWith("```"))
        {
            var firstLineEnd = content.IndexOf('\n');
            if (firstLineEnd != -1)
            {
                content = content[(firstLineEnd + 1)..];
            }
            if (content.EndsWith("```"))
            {
                content = content[..^3];
            }
            content = content.Trim();
        }

        // 2. Remove leading/trailing quotes if AI wrapped the whole thing in a string
        if (content.Length >= 2 && content.StartsWith("\"") && content.EndsWith("\""))
        {
            content = content[1..^1];
        }

        // 3. Handle literal \n and \\ if AI still escaped them
        // If it starts with an escaped backslash for documentclass, it's definitely escaped
        if (content.StartsWith("\\\\documentclass") || (content.Contains("\\n") && !content.Contains("\n")))
        {
            content = content
                .Replace("\\n", "\n")
                .Replace("\\\\", "\\")
                .Replace("\\\"", "\"")
                .Replace("\\&", "&") // Common escapes that might be doubled
                .Replace("\\#", "#")
                .Replace("\\$", "$");
                
            // Note: Since we are unescaping \\ to \, we must be careful with legitimate LaTeX line breaks \\.
            // If the AI sent \\\\, it becomes \\ (correct LaTeX break).
        }
        
        return content.Trim();
    }
}
