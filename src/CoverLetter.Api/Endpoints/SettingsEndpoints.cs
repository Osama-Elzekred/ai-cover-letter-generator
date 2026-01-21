using CoverLetter.Api.Extensions;
using CoverLetter.Application.Common.Interfaces;
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
  private static readonly TimeSpan ApiKeyCacheDuration = TimeSpan.FromDays(30);

  public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes
        .MapGroup("/settings")
        .WithTags("Settings");

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
        .WithSummary("Save custom prompt for CV/Letter/Match")
        .WithDescription("Store a custom prompt for cv-customization, cover-letter, or match-analysis. Requires X-User-Id header.")
        .Produces<SavePromptResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

    group.MapGet("/prompts/{promptType}", GetCustomPrompt)
        .WithSummary("Get custom prompt")
        .WithDescription("Retrieve a custom prompt for the specified type. Returns 404 if none saved.")
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
  /// POST /api/v1/settings/groq-api-key
  /// Saves a user's Groq API key to cache.
  /// </summary>
  private static IResult SaveGroqApiKey(
    [FromBody] SaveApiKeyRequest request,
    HttpContext httpContext,
    IMemoryCache cache,
    ICacheKeyBuilder cacheKeyBuilder)
  {
    var userId = httpContext.GetRequiredUserId();  // Throws if X-User-Id header missing

    if (string.IsNullOrWhiteSpace(request.ApiKey))
    {
      return Result<SaveApiKeyResponse>.ValidationError("ApiKey is required.").ToHttpResult();
    }

    // Basic validation: Groq API keys start with "gsk_"
    if (!request.ApiKey.StartsWith("gsk_", StringComparison.OrdinalIgnoreCase))
    {
      return Result<SaveApiKeyResponse>.ValidationError(
          "Invalid Groq API key format. Keys should start with 'gsk_'.").ToHttpResult();
    }

    var cacheKey = cacheKeyBuilder.UserApiKey(userId);
    cache.Set(cacheKey, request.ApiKey, new MemoryCacheEntryOptions
    {
      AbsoluteExpirationRelativeToNow = ApiKeyCacheDuration
      // Size not needed when SizeLimit is disabled
    });

    var response = new SaveApiKeyResponse(
        Message: "Groq API key saved successfully. Your key will be used for all cover letter generation requests.",
        UserId: userId,
        ExpiresIn: $"{ApiKeyCacheDuration.TotalDays} days"
    );

    return Result<SaveApiKeyResponse>.Success(response).ToHttpResult();
  }

  /// <summary>
  /// GET /api/v1/settings/groq-api-key
  /// Checks if a user has saved their Groq API key.
  /// </summary>
  private static IResult CheckGroqApiKey(
      HttpContext httpContext,
      IMemoryCache cache,
      ICacheKeyBuilder cacheKeyBuilder)
  {
    var userId = httpContext.GetRequiredUserId();  // Throws if X-User-Id header missing

    var cacheKey = cacheKeyBuilder.UserApiKey(userId);
    var hasKey = cache.TryGetValue<string>(cacheKey, out var apiKey);

    if (!hasKey || string.IsNullOrWhiteSpace(apiKey))
    {
      var response = new ApiKeyStatusResponse(
          HasKey: false,
          MaskedKey: null,
          Message: "No API key saved. Using default key with rate limiting.");
      return Result<ApiKeyStatusResponse>.Success(response).ToHttpResult();
    }

    // Mask the key: show first 7 chars and last 4 chars
    var maskedKey = apiKey.Length > 11
        ? $"{apiKey[..7]}...{apiKey[^4..]}"
        : "gsk_***";

    var successResponse = new ApiKeyStatusResponse(
        HasKey: true,
        MaskedKey: maskedKey,
        Message: "Your personal API key is active. No rate limits apply.");

    return Result<ApiKeyStatusResponse>.Success(successResponse).ToHttpResult();
  }

  /// <summary>
  /// DELETE /api/v1/settings/groq-api-key
  /// Deletes a user's saved Groq API key.
  /// </summary>
  private static IResult DeleteGroqApiKey(
      HttpContext httpContext,
      IMemoryCache cache,
      ICacheKeyBuilder cacheKeyBuilder)
  {
    var userId = httpContext.GetRequiredUserId();  // Throws if X-User-Id header missing

    var cacheKey = cacheKeyBuilder.UserApiKey(userId);
    cache.Remove(cacheKey);

    var response = new DeleteApiKeyResponse(
        Message: "Groq API key removed. Future requests will use the default key with rate limiting.",
        UserId: userId
    );

    return Result<DeleteApiKeyResponse>.Success(response).ToHttpResult();
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

    // Validate prompt type
    var allowedTypes = new[] { "cv-customization", "cover-letter", "match-analysis" };
    if (!allowedTypes.Contains(promptType))
      return Result<SavePromptResponse>.ValidationError(
          $"Invalid prompt type. Allowed: {string.Join(", ", allowedTypes)}").ToHttpResult();

    // Map kebab-case to PromptType enum
    var promptTypeEnum = promptType switch
    {
      "cv-customization" => PromptType.CvCustomization,
      "cover-letter" => PromptType.CoverLetter,
      "match-analysis" => PromptType.MatchAnalysis,
      _ => (PromptType?)null
    };

    if (promptTypeEnum == null)
      return Result<SavePromptResponse>.ValidationError("Invalid prompt type").ToHttpResult();

    await customPromptService.SaveUserPromptAsync(promptTypeEnum.Value, request.Prompt, httpContext.RequestAborted);

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

    var promptTypeEnum = promptType switch
    {
      "cv-customization" => PromptType.CvCustomization,
      "cover-letter" => PromptType.CoverLetter,
      "match-analysis" => PromptType.MatchAnalysis,
      _ => (PromptType?)null
    };

    if (promptTypeEnum == null)
      return Result<CustomPromptResponse>.ValidationError("Invalid prompt type").ToHttpResult();

    var prompt = await customPromptService.GetUserPromptAsync(promptTypeEnum.Value, httpContext.RequestAborted);

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

    var promptTypeEnum = promptType switch
    {
      "cv-customization" => PromptType.CvCustomization,
      "cover-letter" => PromptType.CoverLetter,
      "match-analysis" => PromptType.MatchAnalysis,
      _ => (PromptType?)null
    };

    if (promptTypeEnum == null)
      return Result<DeletePromptResponse>.ValidationError("Invalid prompt type").ToHttpResult();

    await customPromptService.DeleteUserPromptAsync(promptTypeEnum.Value, httpContext.RequestAborted);

    var response = new DeletePromptResponse(
        Message: $"Custom prompt for {promptType} deleted successfully. Will use default prompt.",
        UserId: userId,
        PromptType: promptType
    );

    return Result<DeletePromptResponse>.Success(response).ToHttpResult();
  }
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


