using System.Text.Json.Serialization;

namespace CoverLetter.Infrastructure.LlmProviders.Groq;

/// <summary>
/// Groq API request model (OpenAI-compatible chat completions).
/// </summary>
public sealed record GroqChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] List<GroqMessage> Messages,
    [property: JsonPropertyName("temperature")] double Temperature = 0.7,
    [property: JsonPropertyName("max_tokens")] int MaxTokens = 4096
);

/// <summary>
/// A single message in the chat conversation.
/// </summary>
public sealed record GroqMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content
);

/// <summary>
/// Groq API response model.
/// </summary>
public sealed record GroqChatResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("object")] string Object,
    [property: JsonPropertyName("created")] long Created,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("choices")] List<GroqChoice> Choices,
    [property: JsonPropertyName("usage")] GroqUsage Usage
);

/// <summary>
/// A single choice from the completion.
/// </summary>
public sealed record GroqChoice(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("message")] GroqMessage Message,
    [property: JsonPropertyName("finish_reason")] string FinishReason
);

/// <summary>
/// Token usage information.
/// </summary>
public sealed record GroqUsage(
    [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
    [property: JsonPropertyName("completion_tokens")] int CompletionTokens,
    [property: JsonPropertyName("total_tokens")] int TotalTokens
);
