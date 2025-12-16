using CoverLetter.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;
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

  public async Task<LlmResponse> GenerateAsync(
      string prompt,
      LlmGenerationOptions? options = null,
      CancellationToken cancellationToken = default)
  {
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    // Determine which API key to use: user's key (BYOK) or default app key
    if (options?.ApiKey != null)
    {
      _logger.LogDebug("Using user's Groq API key for request.");
    }
    var apiKey = options?.ApiKey ?? _settings.ApiKey;
    var isUserKey = !string.IsNullOrWhiteSpace(options?.ApiKey);

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

    var response = await groqApi.ChatCompletionAsync(request, cancellationToken);
    stopwatch.Stop();

    var content = response.Choices.FirstOrDefault()?.Message.Content
        ?? throw new InvalidOperationException("No content returned from Groq API.");

    _logger.LogInformation(
        "Groq API responded in {ElapsedMs}ms - Model: {Model}, Tokens: {PromptTokens}→{CompletionTokens} (Using {KeyType} key)",
        stopwatch.ElapsedMilliseconds,
        response.Model,
        response.Usage.PromptTokens,
        response.Usage.CompletionTokens,
        isUserKey ? "user's" : "default");

    return new LlmResponse(
        Content: content,
        Model: response.Model,
        PromptTokens: response.Usage.PromptTokens,
        CompletionTokens: response.Usage.CompletionTokens
    );
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
