using CoverLetter.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Infrastructure.LlmProviders;

/// <summary>
/// Decorator over ILlmService that logs the full prompt and response at Debug level.
///
/// WHY a decorator?
///   - Single place — covers every caller (cover letter, CV customization, match analysis,
///     textarea answer) without touching any handler.
///   - Zero changes to business logic — handlers stay clean.
///   - Logging is OFF by default (switch starts at Information). Enable at runtime via
///     PUT /api/v1/debug/llm-log-level with body { "level": "Debug" } — no restart needed.
///   - This is the standard observability pattern used by companies like Stripe/Shopify for
///     wrapping external service calls (interceptor / decorator at the integration boundary).
/// </summary>
public sealed class LoggingLlmService(
    ILlmService inner,
    ILogger<LoggingLlmService> logger) : ILlmService
{
    public async Task<LlmResponse> GenerateAsync(
        string prompt,
        LlmGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                """
                ── LLM REQUEST ────────────────────────────────────────────
                System : {SystemMessage}
                ───────────────────────────────────────────────────────────
                {Prompt}
                ───────────────────────────────────────────────────────────
                """,
                options?.SystemMessage ?? "(none)",
                prompt);
        }

        var response = await inner.GenerateAsync(prompt, options, cancellationToken);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                """
                ── LLM RESPONSE ───────────────────────────────────────────
                Model  : {Model}  |  Tokens: {PromptTokens}→{CompletionTokens}
                ───────────────────────────────────────────────────────────
                {Content}
                ───────────────────────────────────────────────────────────
                """,
                response.Model,
                response.PromptTokens,
                response.CompletionTokens,
                response.Content);
        }

        return response;
    }
}
