using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;
using System.Net;
using System.Net.Http.Headers;

namespace CoverLetter.Infrastructure.LlmProviders.Groq;

/// <summary>
/// Groq implementation of ILlmService using Refit.
/// Supports dynamic API keys for BYOK (Bring Your Own Key) pattern.
/// </summary>
public sealed class GroqLlmService : ILlmService
{
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly GroqSettings _settings;
  private readonly ILogger<GroqLlmService> _logger;

  public GroqLlmService(
      IHttpClientFactory httpClientFactory,
      IOptions<GroqSettings> settings,
      ILogger<GroqLlmService> logger)
  {
    _httpClientFactory = httpClientFactory;
    _settings = settings.Value;
    _logger = logger;
  }

  public async Task<Result<LlmResponse>> GenerateAsync(
      string prompt,
      LlmGenerationOptions? options = null,
      CancellationToken cancellationToken = default)
  {
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    // Determine which API key to use: user's key (BYOK) or default app key
    var isUserKey = !string.IsNullOrWhiteSpace(options?.ApiKey);
    var apiKey = options?.ApiKey ?? _settings.ApiKey;

    _logger.LogDebug(
        "Groq key selection -> source: {KeySource}, key: {MaskedKey}",
        isUserKey ? "user" : "default",
        MaskApiKey(apiKey));

    // Create Groq API client with appropriate API key
    var groqApi = CreateGroqApiClient(apiKey);

    // Build messages list - add system message if provided
    var messages = new List<GroqMessage>();
    if (!string.IsNullOrWhiteSpace(options?.SystemMessage))
    {
      messages.Add(new GroqMessage("system", options.SystemMessage));
    }
    messages.Add(new GroqMessage("user", prompt));

    var request = new GroqChatRequest(
        Model: _settings.Model,
        Messages: messages,
        Temperature: options?.Temperature ?? _settings.Temperature,
        MaxTokens: options?.MaxTokens ?? _settings.MaxTokens
    );

    try
    {
      var response = await groqApi.ChatCompletionAsync(request, cancellationToken);
      stopwatch.Stop();

      var content = response.Choices.FirstOrDefault()?.Message.Content
          ?? throw new InvalidOperationException("No content returned from Groq API.");

      _logger.LogDebug(
        "Groq API responded in {ElapsedMs}ms - Model: {Model}, Tokens: {PromptTokens}→{CompletionTokens} (Using {KeyType} key)",
        stopwatch.ElapsedMilliseconds,
        response.Model,
        response.Usage.PromptTokens,
        response.Usage.CompletionTokens,
        isUserKey ? "user's" : "default");

      return Result.Success(new LlmResponse(
          Content: content,
          Model: response.Model,
          PromptTokens: response.Usage.PromptTokens,
          CompletionTokens: response.Usage.CompletionTokens
      ));
    }
    catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
    {
      stopwatch.Stop();
      _logger.LogWarning(ex, "Groq provider rate limit hit after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
      return Result.Failure<LlmResponse>(
          "AI provider rate limit reached. Please retry in a few seconds, or use your own API key in Settings (BYOK) for higher limits.",
          ResultType.TooManyRequests);
    }
  }

  private static string MaskApiKey(string apiKey)
  {
    if (string.IsNullOrWhiteSpace(apiKey)) return "(empty)";
    return apiKey.Length > 11 ? $"{apiKey[..7]}...{apiKey[^4..]}" : "gsk_***";
  }

  /// <summary>
  /// Creates a Groq API client with the specified API key.
  /// Uses HttpClientFactory for proper lifecycle management.
  /// </summary>
  private IGroqApi CreateGroqApiClient(string apiKey)
  {
    var httpClient = _httpClientFactory.CreateClient("GroqClient");
    httpClient.BaseAddress = new Uri(_settings.BaseUrl);
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    httpClient.Timeout = TimeSpan.FromSeconds(120);

    return RestService.For<IGroqApi>(httpClient);
  }
}
