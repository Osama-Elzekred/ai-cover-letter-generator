using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CoverLetter.Application.UseCases.MatchCv;

public sealed class MatchCvHandler : IRequestHandler<MatchCvCommand, Result<MatchCvResult>>
{
    private readonly IMemoryCache _cache;
    private readonly ILlmService _llmService;
    private readonly IUserContext _userContext;
    private readonly ILogger<MatchCvHandler> _logger;

    public MatchCvHandler(
        IMemoryCache cache,
        ILlmService llmService,
        IUserContext userContext,
        ILogger<MatchCvHandler> logger)
    {
        _cache = cache;
        _llmService = llmService;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<Result<MatchCvResult>> Handle(MatchCvCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var cacheKey = $"cv:{request.CvId}";
            if (!_cache.TryGetValue<CvDocument>(cacheKey, out var cvDocument) || cvDocument is null)
            {
                return Result<MatchCvResult>.Failure("CV not found or expired.", ResultType.NotFound);
            }

            var prompt = $@"
Analyze the compatibility between the following CV and Job Description.
Return the result ONLY as a JSON object with these fields:
- matchScore: (integer, 0-100)
- matchingKeywords: (array of strings, key technical/soft skills present in both)
- missingKeywords: (array of strings, important skills mentioned in the job but missing in the CV)
- analysisSummary: (short 2-sentence summary of the fit)

JOB DESCRIPTION:
{request.JobDescription}

CV CONTENT:
{cvDocument.ExtractedText}

Output only valid JSON:
";

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
