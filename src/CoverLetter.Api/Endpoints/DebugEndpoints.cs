using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Repositories;
using CoverLetter.Application.UseCases.GenerateCoverLetter;
using CoverLetter.Domain.Enums;
using Serilog.Core;
using Serilog.Events;
using System.Text.RegularExpressions;

namespace CoverLetter.Api.Endpoints;

/// <summary>
/// Development-only debug endpoints.
/// Registered only when app.Environment.IsDevelopment() is true.
///
/// Endpoints:
///   GET  /api/v1/debug/llm-log-level    → current log level
///   PUT  /api/v1/debug/llm-log-level    → change level at runtime (no restart)
///   POST /api/v1/debug/prompt-preview   → exact prompt for ANY LLM use-case, no tokens spent
///
/// promptType values:  CoverLetter | CvCustomization | MatchAnalysis | TextareaAnswer
/// </summary>
public static class DebugEndpoints
{
    public static IEndpointRouteBuilder MapDebugEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/debug")
            .WithTags("Debug (Dev Only)");

        // ── Log-level switch ────────────────────────────────────────────────
        group.MapGet("/llm-log-level", (LoggingLevelSwitch levelSwitch) =>
            Results.Ok(new { current = levelSwitch.MinimumLevel.ToString() }))
            .WithSummary("Get current LLM log level");

        group.MapPut("/llm-log-level", (SetLogLevelRequest req, LoggingLevelSwitch levelSwitch) =>
        {
            if (!Enum.TryParse<LogEventLevel>(req.Level, ignoreCase: true, out var parsed))
                return Results.BadRequest(new { error = $"Invalid level '{req.Level}'. Valid: Verbose, Debug, Information, Warning, Error, Fatal" });

            levelSwitch.MinimumLevel = parsed;
            return Results.Ok(new { set = parsed.ToString() });
        })
        .WithSummary("Change LLM log level at runtime — no restart needed")
        .WithDescription("""
            Set to **Debug** to see full prompt + response in the console (no restart needed).
            Set back to **Information** to silence it.
            Valid values: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`
            """);

        // ── Unified prompt preview (all LLM use-cases) ─────────────────────
        group.MapPost("/prompt-preview", async (
            PromptPreviewRequest req,
            IPromptRegistry promptRegistry,
            ICustomPromptService customPromptService,
            ICvRepository cvRepository,
            CancellationToken cancellationToken) =>
        {
            // ── Resolve CV text ─────────────────────────────────────────────
            string? cvText = null;
            if (!string.IsNullOrWhiteSpace(req.CvId))
            {
                if (!Guid.TryParse(req.CvId, out var cvId))
                    return Results.BadRequest(new { error = "Invalid CvId format." });

                var cv = await cvRepository.GetByIdAsync(cvId, cancellationToken);
                if (cv is null)
                    return Results.NotFound(new { error = $"CV not found: {req.CvId}" });

                cvText = cv.Content;
            }
            else if (!string.IsNullOrWhiteSpace(req.CvText))
            {
                cvText = req.CvText;
            }

            // ── Build prompt matching each handler exactly ──────────────────
            string prompt;
            string systemMessage;

            switch (req.PromptType)
            {
                // ── Cover Letter (mirrors GenerateCoverLetterHandler) ───────
                case PromptType.CoverLetter:
                    {
                        if (cvText is null) return Results.BadRequest(new { error = "CvId or CvText is required for CoverLetter." });
                        if (string.IsNullOrWhiteSpace(req.JobDescription)) return Results.BadRequest(new { error = "JobDescription is required for CoverLetter." });

                        systemMessage = "You are a professional cover letter writer. Respond only with the cover letter text, no additional commentary.";
                        var saved = await customPromptService.GetUserPromptAsync(PromptType.CoverLetter, cancellationToken);
                        var vars = new Dictionary<string, string> { { "JobDescription", req.JobDescription! }, { "CvText", cvText } };
                        string ResolveVars(string t) => vars.Aggregate(t, (c, kv) => c.Replace("{" + kv.Key + "}", kv.Value));

                        if (!string.IsNullOrWhiteSpace(req.CustomPromptTemplate))
                        {
                            // Inline prompt — respect PromptMode
                            var resolved = ResolveVars(req.CustomPromptTemplate);
                            if (req.PromptMode == PromptMode.Override)
                            {
                                if (!req.CustomPromptTemplate.Contains("{CvText}"))
                                    resolved += $"\n\nCANDIDATE'S CV (use this for all personal details, name, and sign-off):\n{cvText}";
                                prompt = resolved;
                            }
                            else
                            {
                                // Append: saved prompt as base (if exists), else default
                                string basePrompt;
                                if (!string.IsNullOrWhiteSpace(saved))
                                {
                                    basePrompt = ResolveVars(saved);
                                }
                                else
                                {
                                    var r = promptRegistry.GetPrompt(PromptType.CoverLetter, vars);
                                    if (r.IsFailure) return Results.Problem(r.Error);
                                    basePrompt = r.Value!;
                                }
                                prompt = $"{basePrompt}\n\nADDITIONAL INSTRUCTIONS:\n{resolved}";
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(saved))
                        {
                            // Saved prompt — full template → Override, plain text → Append
                            var resolved = ResolveVars(saved);
                            var isFullTemplate = saved.Contains("{JobDescription}") || saved.Contains("{CvText}");
                            if (isFullTemplate)
                            {
                                if (!saved.Contains("{CvText}"))
                                    resolved += $"\n\nCANDIDATE'S CV (use this for all personal details, name, and sign-off):\n{cvText}";
                                prompt = resolved;
                            }
                            else
                            {
                                var r = promptRegistry.GetPrompt(PromptType.CoverLetter, vars);
                                if (r.IsFailure) return Results.Problem(r.Error);
                                prompt = $"{r.Value}\n\nADDITIONAL INSTRUCTIONS:\n{resolved}";
                            }
                        }
                        else
                        {
                            var r = promptRegistry.GetPrompt(PromptType.CoverLetter, vars);
                            if (r.IsFailure) return Results.Problem(r.Error);
                            prompt = r.Value!;
                        }
                        break;
                    }

                // ── CV Customization (mirrors CustomizeCvHandler) ───────────
                case PromptType.CvCustomization:
                    {
                        if (cvText is null) return Results.BadRequest(new { error = "CvId or CvText is required for CvCustomization." });
                        if (string.IsNullOrWhiteSpace(req.JobDescription)) return Results.BadRequest(new { error = "JobDescription is required for CvCustomization." });

                        systemMessage = "You are a professional LaTeX CV expert. Respond ONLY with raw LaTeX code, no extra text, no markdown fences.";

                        var cleanedJd = Regex.Replace(req.JobDescription!, @"[^\x00-\x7F]", "");
                        var confirmedSkills = req.SelectedKeywords?.Any() == true
                            ? $"**CONFIRMED SKILLS**: The user has confirmed: {string.Join(", ", req.SelectedKeywords!)}. Ensure these are clearly integrated."
                            : "";

                        var vars = new Dictionary<string, string>
                    {
                        { "JobDescription", cleanedJd },
                        { "CvText", cvText },
                        { "ConfirmedSkills", confirmedSkills }
                    };

                        var saved = await customPromptService.GetUserPromptAsync(PromptType.CvCustomization, cancellationToken);
                        var hasInline = !string.IsNullOrWhiteSpace(req.CustomPromptTemplate);

                        if (hasInline)
                        {
                            if (req.PromptMode == PromptMode.Override)
                            {
                                prompt = vars.Aggregate(req.CustomPromptTemplate!, (c, kv) => c.Replace("{" + kv.Key + "}", kv.Value));
                            }
                            else // Append
                            {
                                var baseStr = !string.IsNullOrWhiteSpace(saved) ? saved : (promptRegistry.GetPrompt(PromptType.CvCustomization, vars).Value ?? "");
                                prompt = vars.Aggregate(baseStr, (c, kv) => c.Replace("{" + kv.Key + "}", kv.Value))
                                         + $"\n\n### ADDITIONAL USER INSTRUCTIONS:\n{req.CustomPromptTemplate}";
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(saved))
                        {
                            prompt = vars.Aggregate(saved, (c, kv) => c.Replace("{" + kv.Key + "}", kv.Value));
                        }
                        else
                        {
                            var r = promptRegistry.GetPrompt(PromptType.CvCustomization, vars);
                            if (r.IsFailure) return Results.Problem(r.Error);
                            prompt = r.Value!;
                        }
                        break;
                    }

                // ── Match Analysis (mirrors MatchCvHandler) ─────────────────
                case PromptType.MatchAnalysis:
                    {
                        if (cvText is null) return Results.BadRequest(new { error = "CvId or CvText is required for MatchAnalysis." });
                        if (string.IsNullOrWhiteSpace(req.JobDescription)) return Results.BadRequest(new { error = "JobDescription is required for MatchAnalysis." });

                        systemMessage = "You are an expert recruiter AI. Analyze job compatibility accurately.";
                        var vars = new Dictionary<string, string> { { "JobDescription", req.JobDescription! }, { "CvText", cvText } };

                        var r = promptRegistry.GetPrompt(PromptType.MatchAnalysis, vars);
                        if (r.IsFailure) return Results.Problem(r.Error);

                        var saved = await customPromptService.GetUserPromptAsync(PromptType.MatchAnalysis, cancellationToken);
                        if (string.IsNullOrWhiteSpace(saved))
                        {
                            prompt = r.Value!;
                        }
                        else
                        {
                            var resolvedCustom = vars.Aggregate(saved, (c, kv) => c.Replace("{" + kv.Key + "}", kv.Value));
                            var isFullTemplate = saved.Contains("{JobDescription}") || saved.Contains("{CvText}");
                            prompt = isFullTemplate
                                ? resolvedCustom
                                : $"{r.Value}\n\nADDITIONAL INSTRUCTIONS:\n{resolvedCustom}";
                        }
                        break;
                    }

                // ── Textarea Answer (mirrors AnswerQuestionHandler) ──────────
                case PromptType.TextareaAnswer:
                    {
                        if (cvText is null) return Results.BadRequest(new { error = "CvId or CvText is required for TextareaAnswer." });
                        if (string.IsNullOrWhiteSpace(req.FieldLabel)) return Results.BadRequest(new { error = "FieldLabel is required for TextareaAnswer." });
                        if (string.IsNullOrWhiteSpace(req.UserQuestion)) return Results.BadRequest(new { error = "UserQuestion is required for TextareaAnswer." });

                        systemMessage = "You are an expert resume writer and career coach. Generate a concise, professional answer to the given question using the provided CV information. Keep responses between 50-500 characters. Respond only with the answer text, no additional commentary.";

                        if (!string.IsNullOrWhiteSpace(req.CustomPromptTemplate))
                        {
                            var vars = new Dictionary<string, string>
                        {
                            { "FieldLabel", req.FieldLabel! },
                            { "UserQuestion", req.UserQuestion! },
                            { "CvText", cvText }
                        };
                            if (!string.IsNullOrWhiteSpace(req.JobDescription))
                            {
                                vars["JobTitle"] = req.JobTitle ?? "";
                                vars["CompanyName"] = req.CompanyName ?? "";
                                vars["JobDescription"] = req.JobDescription;
                            }
                            prompt = vars.Aggregate(req.CustomPromptTemplate, (c, kv) => c.Replace("{" + kv.Key + "}", kv.Value));
                        }
                        else
                        {
                            var r = promptRegistry.GetPrompt(PromptType.TextareaAnswer, new Dictionary<string, string>
                        {
                            { "FieldLabel", req.FieldLabel! },
                            { "UserQuestion", req.UserQuestion! },
                            { "CvText", cvText }
                        });
                            prompt = r.IsSuccess ? r.Value! : $"Field: {req.FieldLabel}\nQuestion: {req.UserQuestion}\n\nCV:\n{cvText}";
                        }
                        break;
                    }

                default:
                    return Results.BadRequest(new { error = $"Unknown promptType '{req.PromptType}'. Valid: CoverLetter, CvCustomization, MatchAnalysis, TextareaAnswer" });
            }

            return Results.Ok(new PromptPreviewResponse(
                PromptType: req.PromptType.ToString(),
                SystemMessage: systemMessage,
                Prompt: prompt,
                PromptCharCount: prompt.Length,
                EstimatedTokens: prompt.Length / 4
            ));
        })
        .WithSummary("Preview the exact prompt for ANY LLM use-case — no tokens spent")
        .WithDescription("""
            Builds and returns the fully-resolved prompt without calling the LLM.
            Mirrors the exact prompt-building logic of each handler.

            **promptType** (required): `CoverLetter` | `CvCustomization` | `MatchAnalysis` | `TextareaAnswer`

            | promptType       | Required fields                                  |
            |------------------|--------------------------------------------------|
            | CoverLetter      | jobDescription + (cvId or cvText)                |
            | CvCustomization  | jobDescription + (cvId or cvText)                |
            | MatchAnalysis    | jobDescription + (cvId or cvText)                |
            | TextareaAnswer   | fieldLabel + userQuestion + (cvId or cvText)     |
            """)
        .Produces<PromptPreviewResponse>();

        return routes;
    }
}

public sealed record SetLogLevelRequest(string Level);

/// <summary>
/// Unified prompt-preview request covering all four LLM use-cases.
/// Only supply the fields relevant to your chosen promptType.
/// </summary>
public sealed record PromptPreviewRequest(
    // --- required for all ---
    PromptType PromptType,

    // --- CV source: provide one ---
    string? CvId = null,
    string? CvText = null,

    // --- CoverLetter / CvCustomization / MatchAnalysis ---
    string? JobDescription = null,

    // --- CoverLetter / CvCustomization ---
    string? CustomPromptTemplate = null,
    PromptMode PromptMode = PromptMode.Append,

    // --- CvCustomization only ---
    IEnumerable<string>? SelectedKeywords = null,

    // --- TextareaAnswer only ---
    string? FieldLabel = null,
    string? UserQuestion = null,
    string? JobTitle = null,
    string? CompanyName = null
);

public sealed record PromptPreviewResponse(
    string PromptType,
    string SystemMessage,
    string Prompt,
    int PromptCharCount,
    int EstimatedTokens
);
