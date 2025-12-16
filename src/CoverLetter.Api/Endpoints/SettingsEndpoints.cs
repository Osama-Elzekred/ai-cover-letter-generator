using CoverLetter.Api.Extensions;
using CoverLetter.Domain.Common;
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

    return routes;
  }

  /// <summary>
  /// POST /api/v1/settings/groq-api-key
  /// Saves a user's Groq API key to cache.
  /// </summary>
  private static IResult SaveGroqApiKey(
      [FromBody] SaveApiKeyRequest request,
      HttpContext httpContext,
      IMemoryCache cache)
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

    var cacheKey = GetCacheKey(userId);
    cache.Set(cacheKey, request.ApiKey, new MemoryCacheEntryOptions
    {
      AbsoluteExpirationRelativeToNow = ApiKeyCacheDuration,
      Size = 1
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
      IMemoryCache cache)
  {
    var userId = httpContext.GetRequiredUserId();  // Throws if X-User-Id header missing

    var cacheKey = GetCacheKey(userId);
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
      IMemoryCache cache)
  {
    var userId = httpContext.GetRequiredUserId();  // Throws if X-User-Id header missing

    var cacheKey = GetCacheKey(userId);
    cache.Remove(cacheKey);

    var response = new DeleteApiKeyResponse(
        Message: "Groq API key removed. Future requests will use the default key with rate limiting.",
        UserId: userId
    );

    return Result<DeleteApiKeyResponse>.Success(response).ToHttpResult();
  }

  private static string GetCacheKey(string userId) => $"user:{userId}:groq-api-key";
}

/// <summary>
/// Request DTO for saving API key.
/// </summary>
public sealed record SaveApiKeyRequest(string ApiKey);

/// <summary>
/// Response DTO for saving API key operation.
/// </summary>
public sealed record SaveApiKeyResponse(
    string Message,
    string UserId,
    string ExpiresIn
);

/// <summary>
/// Response DTO for API key status check.
/// </summary>
public sealed record ApiKeyStatusResponse(
    bool HasKey,
    string? MaskedKey,
    string Message
);

/// <summary>
/// Response DTO for deleting API key operation.
/// </summary>
public sealed record DeleteApiKeyResponse(
    string Message,
    string UserId
);
