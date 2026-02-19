using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CoverLetter.Application.UseCases.MatchCv;

public sealed class MatchCvHandler : IRequestHandler<MatchCvCommand, Result<MatchCvResult>>
{
    private readonly ICvRepository _cvRepository;
    private readonly ILlmService _llmService;
    private readonly IUserContext _userContext;
    private readonly IPromptRegistry _promptRegistry;
    private readonly ICustomPromptService _customPromptService;
    private readonly ILogger<MatchCvHandler> _logger;

    public MatchCvHandler(
        ICvRepository cvRepository,
        ILlmService llmService,
        IUserContext userContext,
        IPromptRegistry promptRegistry,
        ICustomPromptService customPromptService,
        ILogger<MatchCvHandler> logger)
    {
        _cvRepository = cvRepository;
        _llmService = llmService;
        _userContext = userContext;
        _promptRegistry = promptRegistry;
        _customPromptService = customPromptService;
        _logger = logger;
    }

    public async Task<Result<MatchCvResult>> Handle(MatchCvCommand request, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["UserId"] = _userContext.UserId ?? "anonymous",
                ["CvId"] = request.CvId
            });

            var cv = await _cvRepository.GetByIdAsync(request.CvId, cancellationToken);
            if (cv is null)
            {
                _logger.LogWarning("CV not found for matching: {CvId}", request.CvId);
                return Result<MatchCvResult>.Failure($"CV not found: {request.CvId}", ResultType.NotFound);
            }

            // Fetch saved custom prompt from settings if available
            var savedCustomPrompt = await _customPromptService.GetUserPromptAsync(PromptType.MatchAnalysis, cancellationToken);

            var variables = new Dictionary<string, string>
            {
                { "JobDescription", request.JobDescription },
                { "CvText", cv.Content }
            };

            // If a saved custom prompt exists and it contains template variables
            // ({JobDescription} / {CvText}), it IS the full prompt — use it directly
            // (Override behaviour) instead of appending it to the default, which would
            // double the entire JD + CV content.
            // If it contains no variables, treat it as additional instructions (Append).
            string prompt;
            if (!string.IsNullOrWhiteSpace(savedCustomPrompt))
            {
                var resolvedCustom = variables.Aggregate(savedCustomPrompt, (c, kv) =>
                    c.Replace("{" + kv.Key + "}", kv.Value));

                var isFullTemplate = savedCustomPrompt.Contains("{JobDescription}") ||
                                     savedCustomPrompt.Contains("{CvText}");

                if (isFullTemplate)
                {
                    // Saved prompt is a full template — use it as the entire prompt
                    prompt = resolvedCustom;
                }
                else
                {
                    // Saved prompt is just extra instructions — append to default
                    var baseResult = _promptRegistry.GetPrompt(PromptType.MatchAnalysis, variables);
                    if (baseResult.IsFailure)
                        return Result<MatchCvResult>.Failure(baseResult.Errors, baseResult.Type);

                    prompt = $"{baseResult.Value}\n\nADDITIONAL INSTRUCTIONS:\n{resolvedCustom}";
                }
            }
            else
            {
                var promptResult = _promptRegistry.GetPrompt(PromptType.MatchAnalysis, variables);
                if (promptResult.IsFailure)
                    return Result<MatchCvResult>.Failure(promptResult.Errors, promptResult.Type);

                prompt = promptResult.Value!;
            }

            var options = new LlmGenerationOptions(
                SystemMessage: "You are an expert recruiter AI. Analyze job compatibility accurately.",
                ApiKey: _userContext.GetUserApiKey()
            );

            var response = await _llmService.GenerateAsync(prompt, options, cancellationToken);
            var result = ParseLlmResponse(response.Content);

            if (result == null)
            {
                return Result<MatchCvResult>.Failure("Failed to parse AI analysis.");
            }

            return Result.Success(new MatchCvResult(
                result.MatchScore,
                result.MatchingKeywords,
                result.MissingKeywords,
                result.AnalysisSummary,
                response.Model,
                response.PromptTokens + response.CompletionTokens
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error matching CV {CvId}", request.CvId);
            return Result<MatchCvResult>.Failure($"Analysis failed: {ex.Message}");
        }
    }

    private MatchCvJsonResponse? ParseLlmResponse(string content)
    {
        try
        {
            // Clean content from potential markdown fences
            var json = content;
            if (json.Contains("```json"))
            {
                json = json.Split("```json")[1].Split("```")[0].Trim();
            }
            else if (json.Contains("```"))
            {
                json = json.Split("```")[1].Split("```")[0].Trim();
            }

            return JsonSerializer.Deserialize<MatchCvJsonResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private sealed class MatchCvJsonResponse
    {
        public int MatchScore { get; set; }
        public List<string> MatchingKeywords { get; set; } = new();
        public List<string> MissingKeywords { get; set; } = new();
        public string AnalysisSummary { get; set; } = string.Empty;
    }
}
