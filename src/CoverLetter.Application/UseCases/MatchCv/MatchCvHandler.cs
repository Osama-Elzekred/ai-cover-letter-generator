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
    private readonly ICvRepository _cvRepository;
    private readonly ILlmService _llmService;
    private readonly IUserContext _userContext;
    private readonly IPromptRegistry _promptRegistry;
    private readonly ILogger<MatchCvHandler> _logger;

    public MatchCvHandler(
        ICvRepository cvRepository,
        ILlmService llmService,
        IUserContext userContext,
        IPromptRegistry promptRegistry,
        ILogger<MatchCvHandler> logger)
    {
        _cvRepository = cvRepository;
        _llmService = llmService;
        _userContext = userContext;
        _promptRegistry = promptRegistry;
        _logger = logger;
    }

    public async Task<Result<MatchCvResult>> Handle(MatchCvCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var cvResult = await _cvRepository.GetByIdAsync(request.CvId, cancellationToken);
            if (cvResult.IsFailure)
            {
                return Result<MatchCvResult>.Failure(cvResult.Errors, cvResult.Type);
            }
            var cvDocument = cvResult.Value!;

            var variables = new Dictionary<string, string>
            {
                { "JobDescription", request.JobDescription },
                { "CvText", cvDocument.ExtractedText }
            };

            var prompt = _promptRegistry.GetPrompt(PromptType.MatchAnalysis, variables);

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
