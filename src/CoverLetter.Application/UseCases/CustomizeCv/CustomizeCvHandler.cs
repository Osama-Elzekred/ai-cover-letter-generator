using System.Text.RegularExpressions;
using CoverLetter.Application.Common.Extensions;
using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Repositories;
using CoverLetter.Application.UseCases.GenerateCoverLetter;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.UseCases.CustomizeCv;

/// <summary>
/// Handler for CustomizeCvCommand.
/// Uses AI to map CV text into a professional LaTeX template tailored to a job description.
/// </summary>
public sealed class CustomizeCvHandler(
    ILlmService llmService,
    ILatexCompilerService latexCompilerService,
    ICvRepository cvRepository,
    IUserContext userContext,
    IPromptRegistry promptRegistry,
    ICustomPromptService customPromptService,
    ILogger<CustomizeCvHandler> logger)
    : IRequestHandler<CustomizeCvCommand, Result<CustomizeCvResult>>
{
    private const string LatexSystemMessage = "You are a professional LaTeX CV expert. Respond ONLY with raw LaTeX code, no extra text, no markdown fences.";
    private static readonly Regex NonAsciiRe = new(@"[^\x00-\x7F]", RegexOptions.Compiled);
    private static readonly Regex UnicodeWhitespaceRe = new(@"[\u00A0\u1680\u2000-\u200A\u202F\u205F\u3000]", RegexOptions.Compiled);
    private static readonly Regex ZeroWidthCharsRe = new(@"[\u00AD\u200B-\u200D\u2060\uFEFF]", RegexOptions.Compiled);
    private static readonly Regex ResumeDocumentClassRe = new(@"\\documentclass(?:\[[^\]]*\])?\{resume\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ResumeDocumentClassLineRe = new(@"\\documentclass(?:\[[^\]]*\])?\{resume\}[^\n]*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DocumentClassLineRe = new(@"(\\documentclass[^\n]*\n)", RegexOptions.Compiled);
    private static readonly Regex ResumeNameRe = new(@"\\name\s*\{", RegexOptions.Compiled);
    private static readonly Regex ResumeAddressRe = new(@"\\address\s*\{", RegexOptions.Compiled);
    private static readonly Regex ResumeSectionBeginRe = new(@"\\begin\s*\{rSection\}", RegexOptions.Compiled);
    private static readonly Regex ArrayColumnModifierRe = new(@">\s*\{", RegexOptions.Compiled);
    private static readonly Regex HyperrefWithoutOptionsRe = new(@"\\usepackage\{hyperref\}", RegexOptions.Compiled);
    private static readonly Regex HyperrefWithOptionsRe = new(@"\\usepackage\[([^\]]*)\]\{hyperref\}", RegexOptions.Compiled);
    private static readonly Regex SectionCommandRe = new(@"\\section\*?\s*\{", RegexOptions.Compiled);
    private static readonly Regex TabularBeginRe = new(@"^\\begin\{(?:tabular|array|tabularx|longtable|tabulary)\}", RegexOptions.Compiled);
    private static readonly Regex TabularEndRe = new(@"^\\end\{(?:tabular|array|tabularx|longtable|tabulary)\}", RegexOptions.Compiled);
    private static readonly Regex AlignmentBeginRe = new(@"^\\begin\{(?:tabular|array|tabularx|longtable|tabulary|align\*?|eqnarray\*?)\}", RegexOptions.Compiled);
    private static readonly Regex AlignmentEndRe = new(@"^\\end\{(?:tabular|array|tabularx|longtable|tabulary|align\*?|eqnarray\*?)\}", RegexOptions.Compiled);

    public async Task<Result<CustomizeCvResult>> Handle(
        CustomizeCvCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = logger.BeginHandlerScope(userContext, "CustomizeCv", new()
            {
                ["CvId"] = request.CvId,
                ["HasCustomPrompt"] = !string.IsNullOrWhiteSpace(request.CustomPromptTemplate)
            });

            // 1. Resolve CV text
            var cv = await cvRepository.GetByIdAsync(request.CvId, cancellationToken);
            if (cv is null)
            {
                logger.LogWarning("CV not found: {CvId}", request.CvId);
                return Result<CustomizeCvResult>.Failure($"CV not found: {request.CvId}", ResultType.NotFound);
            }

            var variables = BuildPromptVariables(request, cv.Content);

            // Fetch custom prompt from settings if exists
            var savedCustomPrompt = await customPromptService.GetUserPromptAsync(PromptType.CvCustomization, cancellationToken);
            var promptResult = BuildFinalPrompt(request, variables, savedCustomPrompt, promptRegistry, logger);
            if (promptResult.IsFailure)
                return Result<CustomizeCvResult>.Failure(promptResult.Errors, promptResult.Type);

            var finalPrompt = promptResult.Value!;

            var userApiKey = userContext.GetUserApiKey();
            var llmOptions = new LlmGenerationOptions(
                SystemMessage: LatexSystemMessage,
                ApiKey: userApiKey
            );

            if (string.IsNullOrWhiteSpace(finalPrompt))
            {
                logger.LogError("Final prompt is empty after processing");
                return Result<CustomizeCvResult>.Failure("Failed to build prompt for CV customization");
            }

            // Normalise any resume.cls references inside the template section of the prompt
            // so the LLM receives a valid, compilable article-class example to faithfully follow.
            // Without this, the LLM sees \documentclass{resume} and either copies it verbatim
            // or diverges from the layout when switching to article on its own.
            finalPrompt = NormaliseLaTeXCompat(finalPrompt);

            logger.LogDebug("Generating customized LaTeX for CV {CvId}", request.CvId);
            var llmResponse = await llmService.GenerateAsync(finalPrompt, llmOptions, cancellationToken);
            if (llmResponse.IsFailure)
                return Result<CustomizeCvResult>.Failure(llmResponse.Errors, llmResponse.Type);

            var latexSource = SanitiseLatexResponse(llmResponse.Value!.Content);

            // 4. Compile LaTeX to PDF (unless LaTeX only requested)
            byte[]? pdfBytes = null;
            if (!request.ReturnLatexOnly)
            {
                logger.LogDebug("Compiling customized LaTeX to PDF for CV {CvId}", request.CvId);
                pdfBytes = await latexCompilerService.CompileToPdfAsync(latexSource, cancellationToken);
            }

            var result = new CustomizeCvResult(
                PdfContent: pdfBytes,
                LatexSource: latexSource,
                FileName: request.ReturnLatexOnly ? "customized_cv.tex" : "customized_cv.pdf",
                Model: llmResponse.Value!.Model,
                PromptTokens: llmResponse.Value.PromptTokens,
                CompletionTokens: llmResponse.Value.CompletionTokens,
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

    // Precompiled for FixLonelyItems — avoids re-compilation on every request.
    private static readonly Regex BeginListRe = new(
        @"\\begin\{(itemize|enumerate|description|list)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EndListRe = new(
        @"\\end\{(itemize|enumerate|description|list)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BlockBoundaryRe = new(
        @"^\\(section\*?|subsection\*?|subsubsection\*?|chapter\*?|part\*?|begin\{rSection|end\{rSection|end\{document)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TabularColSpecRe = new(
        @"(\\begin\{(?:tabular|array|tabularx|longtable|tabulary)\}(?:\[[^\]]*\])?\{)((?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*)",
        RegexOptions.Compiled);
    private static readonly Regex UnescapedAmpersandRe = new(@"(?<!\\)&", RegexOptions.Compiled);
    private static readonly Regex MalformedEndEnvironmentRe = new(
        @"\\end\{([^}]+)\)",
        RegexOptions.Compiled);

    private static Dictionary<string, string> BuildPromptVariables(CustomizeCvCommand request, string cvText)
    {
        var confirmedSkills = string.Empty;
        if (request.SelectedKeywords != null && request.SelectedKeywords.Any())
        {
            var keywordsList = string.Join(", ", request.SelectedKeywords);
            confirmedSkills = $"**CONFIRMED SKILLS**: The user has specifically confirmed they possess the following skills: {keywordsList}. You MUST ensure these are clearly integrated into the CV.";
        }

        return new Dictionary<string, string>
        {
            ["JobDescription"] = NonAsciiRe.Replace(request.JobDescription, ""),
            ["CvText"] = cvText,
            ["ConfirmedSkills"] = confirmedSkills
        };
    }

    private static Result<string> BuildFinalPrompt(
        CustomizeCvCommand request,
        IReadOnlyDictionary<string, string> variables,
        string? savedCustomPrompt,
        IPromptRegistry promptRegistry,
        ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(request.CustomPromptTemplate) && request.PromptMode == PromptMode.Override)
        {
            logger.LogDebug("Using inline prompt in Override mode (full replacement for this call)");
            return Result<string>.Success(BuildOverridePrompt(request.CustomPromptTemplate, variables));
        }

        var basePromptResult = ResolveBasePrompt(savedCustomPrompt, variables, promptRegistry, logger);
        if (basePromptResult.IsFailure)
            return basePromptResult;

        if (string.IsNullOrWhiteSpace(request.CustomPromptTemplate))
            return basePromptResult;

        logger.LogDebug("Appending inline instructions to base prompt");
        return Result<string>.Success($"{basePromptResult.Value}\n\n### ADDITIONAL USER INSTRUCTIONS:\n{ResolveTemplate(request.CustomPromptTemplate, variables)}");
    }

    private static Result<string> ResolveBasePrompt(
        string? savedCustomPrompt,
        IReadOnlyDictionary<string, string> variables,
        IPromptRegistry promptRegistry,
        ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(savedCustomPrompt))
        {
            logger.LogDebug("Using saved prompt from Settings as base");
            return Result<string>.Success(ResolveTemplate(savedCustomPrompt, variables));
        }

        logger.LogDebug("Using default prompt template as base");
        return promptRegistry.GetPrompt(PromptType.CvCustomization, variables.ToDictionary(x => x.Key, x => x.Value));
    }

    private static string BuildOverridePrompt(string template, IReadOnlyDictionary<string, string> variables)
    {
        var resolved = ResolveTemplate(template, variables);

        if (!template.Contains("{JobDescription}"))
            resolved += $"\n\nJOB DESCRIPTION:\n{variables["JobDescription"]}";
        if (!template.Contains("{CvText}"))
            resolved += $"\n\nCANDIDATE'S CV:\n{variables["CvText"]}";
        if (!string.IsNullOrWhiteSpace(variables["ConfirmedSkills"]) && !template.Contains("{ConfirmedSkills}"))
            resolved += $"\n\n{variables["ConfirmedSkills"]}";

        return resolved;
    }

    private static string ResolveTemplate(string template, IReadOnlyDictionary<string, string> variables)
        => variables.Aggregate(template, (current, entry) => current.Replace("{" + entry.Key + "}", entry.Value));

    /// <summary>
    /// Strips LLM output artefacts (markdown fences, escaped sequences, mis-encoded characters)
    /// and applies compatibility patches so the source compiles in the Docker TeX Live container.
    /// </summary>
    private static string SanitiseLatexResponse(string content)
    {
        content = content.Trim();

        // 1. Remove markdown code fences the model occasionally wraps around the output.
        if (content.StartsWith("```"))
        {
            var firstNewline = content.IndexOf('\n');
            if (firstNewline != -1) content = content[(firstNewline + 1)..];
            if (content.EndsWith("```")) content = content[..^3];
            content = content.Trim();
        }

        // 2. Remove wrapping double-quotes if the model serialised the source as a JSON string.
        if (content.Length >= 2 && content.StartsWith("\"") && content.EndsWith("\""))
            content = content[1..^1];

        // 3. Unescape doubly-escaped sequences emitted when the model ignores the
        //    "do not escape backslashes" instruction.  Guard: only unescape when the
        //    document clearly starts with a doubled backslash or contains literal \n.
        if (content.StartsWith("\\\\documentclass") || (content.Contains("\\n") && !content.Contains("\n")))
        {
            // Unescape order matters: \\\\ → \\ (LaTeX line-break), then remaining \\ → \
            content = content
                .Replace("\\n", "\n")
                .Replace("\\\\", "\\")
                .Replace("\\\"", "\"");
        }

        // 4. Inject missing packages / macros and normalise problematic preamble patterns.
        content = NormaliseLaTeXCompat(content);

        // 5. Auto-wrap bare \item commands that appear outside any list environment.
        content = FixLonelyItems(content);

        // 6. Replace Unicode whitespace variants with ASCII space.  These are invisible
        //    in text but fatal inside tabular column specs ("Illegal character in array arg").
        content = UnicodeWhitespaceRe.Replace(content, " ");

        // 7. Remove zero-width / invisible control characters.
        content = ZeroWidthCharsRe.Replace(content, "");

        // 8. Strip non-ASCII from tabular/array column specs (must be pure ASCII even with
        //    inputenc loaded).  Handles up to two levels of nested braces, e.g. @{\hspace{4ex}}.
        content = TabularColSpecRe.Replace(
            content,
            m =>
            {
                var colSpec = m.Groups[2].Value;
                // Keep valid ASCII column specs unchanged to avoid accidental rewrites.
                if (!NonAsciiRe.IsMatch(colSpec))
                    return m.Value;

                var cleaned = NonAsciiRe.Replace(colSpec, "");
                return m.Groups[1].Value + cleaned + "}";
            });

        // 9. Repair malformed environment endings sometimes emitted by LLMs:
        //    \end{rSection) -> \end{rSection}
        content = FixMalformedEnvironmentEndings(content);

        // 10. Remove blank lines inside tabular-like environments.
        //     Empty lines inside alignment environments commonly cause "Misplaced \cr".
        content = RemoveBlankLinesInsideTabular(content);

        // 11. Escape stray '&' outside alignment environments to avoid
        //    "Misplaced alignment tab character &" fatal errors.
        content = EscapeMisplacedAlignmentTabs(content);

        return content.Trim();
    }

    private static string EscapeMisplacedAlignmentTabs(string content)
    {
        var lines = content.Split('\n');
        var result = new List<string>(lines.Length);
        var alignmentDepth = 0;

        foreach (var line in lines)
        {
            var t = line.TrimStart();

            if (AlignmentBeginRe.IsMatch(t))
                alignmentDepth++;

            // Preserve '&' inside alignment environments as real column/alignment separators.
            result.Add(alignmentDepth > 0 ? line : UnescapedAmpersandRe.Replace(line, @"\&"));

            if (AlignmentEndRe.IsMatch(t))
                alignmentDepth = Math.Max(0, alignmentDepth - 1);
        }

        return string.Join('\n', result);
    }

    private static string FixMalformedEnvironmentEndings(string content)
      => MalformedEndEnvironmentRe.Replace(content, @"\end{$1}");

    private static string RemoveBlankLinesInsideTabular(string content)
    {
        var lines = content.Split('\n');
        var result = new List<string>(lines.Length);
        var tabularDepth = 0;

        foreach (var line in lines)
        {
            var t = line.TrimStart();

            if (TabularBeginRe.IsMatch(t))
                tabularDepth++;

            if (!(tabularDepth > 0 && string.IsNullOrWhiteSpace(line)))
                result.Add(line);

            if (TabularEndRe.IsMatch(t))
                tabularDepth = Math.Max(0, tabularDepth - 1);
        }

        return string.Join('\n', result);
    }

    /// <summary>
    /// Wraps bare \item commands that sit outside any list environment with
    /// \begin{itemize}...\end{itemize}, preventing a fatal "Lonely \item" error.
    /// This happens when LLMs place project/activity items directly under \section*
    /// without an enclosing list, copying the resume.cls pattern where rSection
    /// itself provides the list context.
    /// </summary>
    private static string FixLonelyItems(string content)
    {
        var lines = content.Split('\n');
        var result = new List<string>(lines.Length + 8);
        var depth = 0;
        var inGroup = false;

        foreach (var line in lines)
        {
            var t = line.TrimStart();
            var isItem = t.StartsWith(@"\item");
            var isBound = BlockBoundaryRe.IsMatch(t);

            if (inGroup && depth == 0 && isBound) { result.Add(@"\end{itemize}"); inGroup = false; }
            if (isItem && depth == 0 && !inGroup) { result.Add(@"\begin{itemize}"); inGroup = true; }

            result.Add(line);

            // Update depth after emitting the line to avoid off-by-one on \begin/\end lines.
            if (BeginListRe.IsMatch(t)) depth++;
            if (EndListRe.IsMatch(t)) depth--;
            if (depth < 0) depth = 0;
        }

        if (inGroup) result.Add(@"\end{itemize}");
        return string.Join('\n', result);
    }

    // resume.cls compatibility macros injected whenever the document uses resume.cls
    // commands but the class file itself is unavailable in the Docker TeX Live container.
    // rSection is rendered as a heading block only; list handling is delegated to
    // FixLonelyItems to avoid layout collisions around the first entry.
    private const string ResumeCompatBlock =
        "\n% ── resume.cls compatibility (auto-generated) ──────────────────────────────\n" +
        "\\usepackage{array}\n" +
        "\\pagestyle{empty}\n" +
        "\\makeatletter\n" +
        "\\newcommand{\\resume@name}{}\n" +
        "\\newcommand{\\resume@addresses}{}\n" +
        "\\newcommand{\\name}[1]{\\gdef\\resume@name{#1}}\n" +
        "\\newcommand{\\address}[1]{\\g@addto@macro\\resume@addresses{{\\small #1}\\\\}}\n" +
        "\\AtBeginDocument{%\n" +
        "  \\begin{center}%\n" +
        "    {\\Huge\\bfseries \\resume@name}\\\\[0.35em]%\n" +
        "    \\resume@addresses%\n" +
        "  \\end{center}%\n" +
        "  \\vspace{0.25em}%\n" +
        "}\n" +
        "\\makeatother\n" +
        "\\newenvironment{rSection}[1]{%\n" +
        "  \\vspace{0.5em}{\\large\\bfseries\\uppercase{#1}}\\par\\vspace{0.25em}\\hrule\\vspace{0.55em}%\n" +
        "}{\\vspace{0.2em}}\n" +
        "% ─────────────────────────────────────────────────────────────────────────────\n";

    /// <summary>
    /// Normalises preamble issues introduced by LLMs following resume.cls-based templates.
    /// Safe to run on both the prompt (pre-LLM) and the LLM output (post-LLM).
    /// <list type="number">
    ///   <item>\documentclass{resume} → article + inline macro definitions</item>
    ///   <item>article class with undeclared resume.cls macros (\name / rSection) → inject compat block</item>
    ///   <item>Missing \usepackage{array} when >{ } column specs are used</item>
    ///   <item>\usepackage{hyperref} without hidelinks → adds hidelinks to suppress link boxes</item>
    ///   <item>\section* without titlesec → inject styling that matches resume.cls section appearance</item>
    /// </list>
    /// </summary>
    private static string NormaliseLaTeXCompat(string content)
    {
        // ── 1. \documentclass{resume} → article ────────────────────────────────────
        if (ResumeDocumentClassRe.IsMatch(content))
        {
            content = ResumeDocumentClassLineRe.Replace(content, @"\documentclass[letterpaper,11pt]{article}");

            content = DocumentClassLineRe.Replace(content, m => m.Value + ResumeCompatBlock, 1);
            return NormaliseLaTeXCompat(content); // re-run to apply remaining checks
        }

        // ── 2. Undeclared resume.cls macros in an article document ─────────────────
        // The LLM sometimes outputs \documentclass{article} but keeps \name / \address /
        // \begin{rSection} from the template, which have no definition in article.cls.
        var usesResumeMacros =
            ResumeNameRe.IsMatch(content) ||
            ResumeAddressRe.IsMatch(content) ||
            ResumeSectionBeginRe.IsMatch(content);
        var alreadyDefined =
            content.Contains(@"\newcommand{\name}") ||
            content.Contains(@"\newenvironment{rSection}");

        if (usesResumeMacros && !alreadyDefined)
            content = DocumentClassLineRe.Replace(content, m => m.Value + ResumeCompatBlock, 1);

        // ── 3. \usepackage{array} for >{ } column specs ────────────────────────────
        if (ArrayColumnModifierRe.IsMatch(content) && !content.Contains(@"\usepackage{array}"))
            content = DocumentClassLineRe.Replace(content, m => m.Value + "\\usepackage{array}\n", 1);

        // ── 4. hidelinks on hyperref ────────────────────────────────────────────────
        // Without hidelinks, hyperref draws a coloured rectangle border around every link.
        if (content.Contains("{hyperref}") && !content.Contains("hidelinks"))
        {
            // Try options-less form first; fall through to options form if not matched.
            var replaced = HyperrefWithoutOptionsRe.Replace(content, @"\usepackage[hidelinks]{hyperref}");
            if (ReferenceEquals(replaced, content)) // no match — options form
                replaced = HyperrefWithOptionsRe.Replace(content, @"\usepackage[$1,hidelinks]{hyperref}");
            content = replaced;
        }

        // ── 5. Section hrules via titlesec ─────────────────────────────────────────
        // When the LLM uses \section* instead of rSection, headings have no rule beneath
        // them.  Inject titlesec styling that mirrors resume.cls: large bold uppercase + \titlerule.
        if (SectionCommandRe.IsMatch(content)
            && !content.Contains(@"\usepackage{titlesec}")
            && !content.Contains(@"\titleformat"))
        {
            content = DocumentClassLineRe.Replace(
                content,
                m => m.Value
                   + "\\usepackage{titlesec}\n"
                   + "\\titleformat{\\section}{\\large\\bfseries\\uppercase}{}{0pt}{}[\\titlerule]\n"
                   + "\\titlespacing*{\\section}{0pt}{8pt}{4pt}\n",
                1);
        }

        return content;
    }
}
