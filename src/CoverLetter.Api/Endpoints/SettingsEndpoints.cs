using CoverLetter.Api.Extensions;
using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Repositories;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace CoverLetter.Api.Endpoints;

/// <summary>
/// User settings endpoints for managing API keys and preferences.
/// </summary>
public static class SettingsEndpoints
{
  private const string GroqProviderRoute = "groq";
  private static readonly TimeSpan ApiKeyCacheDuration = TimeSpan.FromDays(30);
  private static readonly IReadOnlyDictionary<string, PromptType> PromptTypeMap =
    new Dictionary<string, PromptType>(StringComparer.OrdinalIgnoreCase)
    {
      ["cv-customization"] = PromptType.CvCustomization,
      ["cover-letter"] = PromptType.CoverLetter,
      ["match-analysis"] = PromptType.MatchAnalysis,
      ["textarea-answer"] = PromptType.TextareaAnswer
    };

  private static readonly string AllowedPromptTypes = string.Join(", ", PromptTypeMap.Keys.OrderBy(x => x));

  private static readonly MemoryCacheEntryOptions ApiKeyCacheEntryOptions = new()
  {
    AbsoluteExpirationRelativeToNow = ApiKeyCacheDuration
  };

  public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes
        .MapGroup("/settings")
        .WithTags("Settings");

    // Provider-agnostic API key routes (future-proof for multi-provider BYOK)
    group.MapPost("/api-keys/{provider}", SaveApiKey)
      .WithSummary("Save user's provider API key")
      .WithDescription("Store a user's personal API key for a specific LLM provider. Requires X-User-Id header.")
      .Produces<SaveApiKeyResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .ProducesProblem(StatusCodes.Status401Unauthorized);

    group.MapGet("/api-keys/{provider}", CheckApiKey)
      .WithSummary("Check if user has saved provider API key")
      .WithDescription("Verify whether the current user has stored their personal API key for a provider. Requires X-User-Id header.")
      .Produces<ApiKeyStatusResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status401Unauthorized);

    group.MapDelete("/api-keys/{provider}", DeleteApiKey)
      .WithSummary("Delete user's saved provider API key")
      .WithDescription("Remove stored API key for a provider. Requires X-User-Id header.")
      .Produces<DeleteApiKeyResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status401Unauthorized);

    // Backward-compatible aliases for existing extension clients
    group.MapPost("/groq-api-key", SaveGroqApiKey)
        .WithSummary("Save user's Groq API key")
        .WithDescription("Store a user's personal Groq API key. Requires X-User-Id header. When saved, the user's key will be used instead of the default, bypassing rate limits.")
        .Produces<SaveApiKeyResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

    group.MapGet("/groq-api-key", CheckGroqApiKey)
        .WithSummary("Check if user has saved Groq API key")
        .WithDescription("Verify whether the current user has stored their personal Groq API key. Requires X-User-Id header. Returns masked key if present.")
        .Produces<ApiKeyStatusResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

    group.MapDelete("/groq-api-key", DeleteGroqApiKey)
        .WithSummary("Delete user's saved Groq API key")
        .WithDescription("Remove the stored Groq API key. Requires X-User-Id header. Future requests will use the default key with rate limiting.")
        .Produces<DeleteApiKeyResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

    // Custom Prompt endpoints
    group.MapPost("/prompts/{promptType}", SaveCustomPrompt)
        .WithSummary("Save custom prompt for CV/Letter/Match/TextareaAnswer")
        .WithDescription("Store a custom prompt for cv-customization, cover-letter, match-analysis, or textarea-answer. Requires X-User-Id header.")
        .Produces<SavePromptResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

    group.MapGet("/prompts/{promptType}", GetCustomPrompt)
        .WithSummary("Get custom prompt")
        .WithDescription("Retrieve a custom prompt for the specified type (cv-customization, cover-letter, match-analysis, textarea-answer). Returns 404 if none saved.")
        .Produces<CustomPromptResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

    group.MapDelete("/prompts/{promptType}", DeleteCustomPrompt)
        .WithSummary("Delete custom prompt")
        .WithDescription("Remove custom prompt and revert to default.")
        .Produces<DeletePromptResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

    return routes;
  }

  /// <summary>
  /// POST /api/v1/settings/api-keys/{provider}
  /// Saves a user's provider API key to DB and cache.
  /// </summary>
  private static async Task<IResult> SaveApiKey(
    string provider,
    [FromBody] SaveApiKeyRequest request,
    HttpContext httpContext,
    IMemoryCache cache,
    ICacheKeyBuilder cacheKeyBuilder,
    IUserApiKeyRepository userApiKeyRepository,
    IUnitOfWork unitOfWork)
  {
    var userId = httpContext.GetRequiredUserId();  // Throws if X-User-Id header missing
    if (!TryParseProvider(provider, out var llmProvider))
      return Result<SaveApiKeyResponse>.ValidationError($"Unsupported provider '{provider}'.").ToHttpResult();

    if (string.IsNullOrWhiteSpace(request.ApiKey))
    {
      return Result<SaveApiKeyResponse>.ValidationError("ApiKey is required.").ToHttpResult();
    }

    if (!IsApiKeyFormatValid(llmProvider, request.ApiKey, out var formatError))
      return Result<SaveApiKeyResponse>.ValidationError(formatError!).ToHttpResult();

    var cacheKey = cacheKeyBuilder.UserApiKey(userId, llmProvider);

    await userApiKeyRepository.UpsertAsync(new UserApiKeyDto
    {
      UserId = userId,
      Provider = llmProvider,
      ApiKey = request.ApiKey
    }, httpContext.RequestAborted);
    await unitOfWork.SaveChangesAsync(httpContext.RequestAborted);

    cache.Set(cacheKey, request.ApiKey, ApiKeyCacheEntryOptions);

    var response = new SaveApiKeyResponse(
      Message: $"{llmProvider} API key saved successfully.",
        UserId: userId,
        ExpiresIn: $"{ApiKeyCacheDuration.TotalDays} days"
    );

    return Result<SaveApiKeyResponse>.Success(response).ToHttpResult();
  }

  /// <summary>
  /// GET /api/v1/settings/api-keys/{provider}
  /// Checks if a user has saved their provider API key.
  /// </summary>
  private static async Task<IResult> CheckApiKey(
      string provider,
      HttpContext httpContext,
      IMemoryCache cache,
      ICacheKeyBuilder cacheKeyBuilder,
      IUserApiKeyRepository userApiKeyRepository)
  {
    var userId = httpContext.GetRequiredUserId();  // Throws if X-User-Id header missing
    if (!TryParseProvider(provider, out var llmProvider))
      return Result<ApiKeyStatusResponse>.ValidationError($"Unsupported provider '{provider}'.").ToHttpResult();

    var cacheKey = cacheKeyBuilder.UserApiKey(userId, llmProvider);
    var hasKey = cache.TryGetValue<string>(cacheKey, out var apiKey);

    if (!hasKey || string.IsNullOrWhiteSpace(apiKey))
    {
      var persisted = await userApiKeyRepository.GetAsync(userId, llmProvider, httpContext.RequestAborted);
      if (!string.IsNullOrWhiteSpace(persisted?.ApiKey))
      {
        apiKey = persisted.ApiKey;
        hasKey = true;
        cache.Set(cacheKey, apiKey, ApiKeyCacheEntryOptions);
      }
    }

    if (!hasKey || string.IsNullOrWhiteSpace(apiKey))
    {
      var response = new ApiKeyStatusResponse(
          HasKey: false,
          MaskedKey: null,
          Message: $"No {llmProvider} API key saved. Using default key with rate limiting.");
      return Result<ApiKeyStatusResponse>.Success(response).ToHttpResult();
    }

    // Mask the key: show first 7 chars and last 4 chars
    var maskedKey = apiKey.Length > 11
        ? $"{apiKey[..7]}...{apiKey[^4..]}"
        : "gsk_***";

    var successResponse = new ApiKeyStatusResponse(
        HasKey: true,
        MaskedKey: maskedKey,
      Message: $"Your personal {llmProvider} API key is active.");

    return Result<ApiKeyStatusResponse>.Success(successResponse).ToHttpResult();
  }

  /// <summary>
  /// DELETE /api/v1/settings/api-keys/{provider}
  /// Deletes a user's saved provider API key.
  /// </summary>
  private static async Task<IResult> DeleteApiKey(
      string provider,
      HttpContext httpContext,
      IMemoryCache cache,
      ICacheKeyBuilder cacheKeyBuilder,
      IUserApiKeyRepository userApiKeyRepository,
      IUnitOfWork unitOfWork)
  {
    var userId = httpContext.GetRequiredUserId();  // Throws if X-User-Id header missing
    if (!TryParseProvider(provider, out var llmProvider))
      return Result<DeleteApiKeyResponse>.ValidationError($"Unsupported provider '{provider}'.").ToHttpResult();

    await userApiKeyRepository.DeleteAsync(userId, llmProvider, httpContext.RequestAborted);
    await unitOfWork.SaveChangesAsync(httpContext.RequestAborted);

    var cacheKey = cacheKeyBuilder.UserApiKey(userId, llmProvider);
    cache.Remove(cacheKey);

    var response = new DeleteApiKeyResponse(
        Message: $"{llmProvider} API key removed. Future requests will use the default key.",
        UserId: userId
    );

    return Result<DeleteApiKeyResponse>.Success(response).ToHttpResult();
  }

  // Legacy aliases for existing clients.
  private static Task<IResult> SaveGroqApiKey(
    [FromBody] SaveApiKeyRequest request,
    HttpContext httpContext,
    IMemoryCache cache,
    ICacheKeyBuilder cacheKeyBuilder,
    IUserApiKeyRepository userApiKeyRepository,
    IUnitOfWork unitOfWork)
    => SaveApiKey(GroqProviderRoute, request, httpContext, cache, cacheKeyBuilder, userApiKeyRepository, unitOfWork);

  private static Task<IResult> CheckGroqApiKey(
      HttpContext httpContext,
      IMemoryCache cache,
      ICacheKeyBuilder cacheKeyBuilder,
      IUserApiKeyRepository userApiKeyRepository)
      => CheckApiKey(GroqProviderRoute, httpContext, cache, cacheKeyBuilder, userApiKeyRepository);

  private static Task<IResult> DeleteGroqApiKey(
      HttpContext httpContext,
      IMemoryCache cache,
      ICacheKeyBuilder cacheKeyBuilder,
      IUserApiKeyRepository userApiKeyRepository,
      IUnitOfWork unitOfWork)
      => DeleteApiKey(GroqProviderRoute, httpContext, cache, cacheKeyBuilder, userApiKeyRepository, unitOfWork);

  private static bool TryParseProvider(string rawProvider, out LlmProvider provider)
    => Enum.TryParse(rawProvider, ignoreCase: true, out provider);

  private static bool IsApiKeyFormatValid(LlmProvider provider, string apiKey, out string? error)
  {
    error = null;

    switch (provider)
    {
      case LlmProvider.Groq:
        if (!apiKey.StartsWith("gsk_", StringComparison.OrdinalIgnoreCase))
        {
          error = "Invalid Groq API key format. Keys should start with 'gsk_'.";
          return false;
        }
        return true;
      default:
        error = $"Validation rule for provider '{provider}' is not configured.";
        return false;
    }
  }

  //===========================================
  // Custom Prompt Handlers
  //===========================================
  // Note: Users can customize full prompts including LaTeX templates
  // via the cv-customization prompt type rather than separate template storage

  private static async Task<IResult> SaveCustomPrompt(
    string promptType,
    [FromBody] SaveCustomPromptRequest request,
    HttpContext httpContext,
    ICustomPromptService customPromptService)
  {
    var userId = httpContext.GetUserId();
    if (string.IsNullOrEmpty(userId))
      return Result<SavePromptResponse>.Unauthorized("User ID is required").ToHttpResult();

    if (string.IsNullOrWhiteSpace(request.Prompt))
      return Result<SavePromptResponse>.ValidationError("Prompt cannot be empty").ToHttpResult();

    if (!TryParsePromptType(promptType, out var promptTypeEnum))
      return Result<SavePromptResponse>.ValidationError($"Invalid prompt type. Allowed: {AllowedPromptTypes}").ToHttpResult();

    await customPromptService.SaveUserPromptAsync(promptTypeEnum, request.Prompt, httpContext.RequestAborted);

    var response = new SavePromptResponse(
        Message: $"Custom prompt for {promptType} saved successfully",
        UserId: userId,
        PromptType: promptType,
        PromptLength: request.Prompt.Length,
        ExpiresIn: "persisted"
    );

    return Result<SavePromptResponse>.Success(response).ToHttpResult();
  }

  private static async Task<IResult> GetCustomPrompt(
      string promptType,
      HttpContext httpContext,
      ICustomPromptService customPromptService)
  {
    var userId = httpContext.GetUserId();
    if (string.IsNullOrEmpty(userId))
      return Result<CustomPromptResponse>.Unauthorized("User ID is required").ToHttpResult();

    if (!TryParsePromptType(promptType, out var promptTypeEnum))
      return Result<CustomPromptResponse>.ValidationError($"Invalid prompt type. Allowed: {AllowedPromptTypes}").ToHttpResult();

    var prompt = await customPromptService.GetUserPromptAsync(promptTypeEnum, httpContext.RequestAborted);

    if (!string.IsNullOrEmpty(prompt))
    {
      var response = new CustomPromptResponse(
          PromptType: promptType,
          Prompt: prompt,
          PromptLength: prompt.Length
      );
      return Result<CustomPromptResponse>.Success(response).ToHttpResult();
    }

    return Result<CustomPromptResponse>.NotFound($"No custom prompt found for {promptType}").ToHttpResult();
  }

  private static async Task<IResult> DeleteCustomPrompt(
    string promptType,
    HttpContext httpContext,
    ICustomPromptService customPromptService)
  {
    var userId = httpContext.GetUserId();
    if (string.IsNullOrEmpty(userId))
      return Result<DeletePromptResponse>.Unauthorized("User ID is required").ToHttpResult();

    if (!TryParsePromptType(promptType, out var promptTypeEnum))
      return Result<DeletePromptResponse>.ValidationError($"Invalid prompt type. Allowed: {AllowedPromptTypes}").ToHttpResult();

    await customPromptService.DeleteUserPromptAsync(promptTypeEnum, httpContext.RequestAborted);

    var response = new DeletePromptResponse(
        Message: $"Custom prompt for {promptType} deleted successfully. Will use default prompt.",
        UserId: userId,
        PromptType: promptType
    );

    return Result<DeletePromptResponse>.Success(response).ToHttpResult();
  }

  private static bool TryParsePromptType(string rawPromptType, out PromptType promptType)
    => PromptTypeMap.TryGetValue(rawPromptType, out promptType);
}

// DTOs for Groq API Key
public sealed record SaveApiKeyRequest(string ApiKey);
public sealed record SaveApiKeyResponse(string Message, string UserId, string ExpiresIn);
public sealed record ApiKeyStatusResponse(bool HasKey, string? MaskedKey, string Message);
public sealed record DeleteApiKeyResponse(string Message, string UserId);

// DTOs for Custom Prompts
public sealed record SaveCustomPromptRequest(string Prompt);
public sealed record SavePromptResponse(string Message, string UserId, string PromptType, int PromptLength, string ExpiresIn);
public sealed record CustomPromptResponse(string PromptType, string Prompt, int PromptLength);
public sealed record DeletePromptResponse(string Message, string UserId, string PromptType);


