using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CoverLetter.Api.Configuration;
using CoverLetter.Api.Models;
using Microsoft.Extensions.Options;

namespace CoverLetter.Api.Services;

/// <summary>
/// HTTP client for communicating with Groq API.
/// </summary>
public sealed class GroqChatClient : IGroqChatClient
{
  private readonly HttpClient _httpClient;
  private readonly GroqSettings _settings;
  private readonly ILogger<GroqChatClient> _logger;

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false
  };

  public GroqChatClient(
      HttpClient httpClient,
      IOptions<GroqSettings> settings,
      ILogger<GroqChatClient> logger)
  {
    _httpClient = httpClient;
    _settings = settings.Value
        ?? throw new ArgumentNullException(nameof(settings), "Groq settings must be provided.");
    _logger = logger;

    _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
    _httpClient.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
    _httpClient.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
  }

  public async Task<GroqChatResponse> ChatCompletionAsync(
      List<GroqMessage> messages,
      CancellationToken cancellationToken = default)
  {
    var request = new GroqChatRequest(
        Model: _settings.Model,
        Messages: messages,
        Temperature: _settings.Temperature,
        MaxTokens: _settings.MaxTokens
    );

    var jsonContent = JsonSerializer.Serialize(request, JsonOptions);
    var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

    _logger.LogInformation("Sending chat completion request to Groq API using model {Model}", _settings.Model);

    var response = await _httpClient.PostAsync("/openai/v1/chat/completions", httpContent, cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
      _logger.LogError("Groq API request failed with status {StatusCode}: {Error}",
          response.StatusCode, errorContent);
      throw new HttpRequestException($"Groq API request failed: {response.StatusCode} - {errorContent}");
    }

    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
    var chatResponse = JsonSerializer.Deserialize<GroqChatResponse>(responseContent, JsonOptions);

    if (chatResponse is null)
    {
      throw new InvalidOperationException("Failed to deserialize Groq API response.");
    }

    _logger.LogInformation(
        "Groq API response received. Tokens used - Prompt: {PromptTokens}, Completion: {CompletionTokens}",
        chatResponse.Usage.PromptTokens,
        chatResponse.Usage.CompletionTokens);

    return chatResponse;
  }
}
