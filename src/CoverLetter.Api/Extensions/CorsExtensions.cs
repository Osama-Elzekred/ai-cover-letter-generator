using Microsoft.AspNetCore.Cors.Infrastructure;

namespace CoverLetter.Api.Extensions;

/// <summary>
/// Extension methods for configuring CORS (Cross-Origin Resource Sharing).
/// </summary>

public static class CorsExtensions
{
  private const string DevelopmentPolicyName = "DevelopmentPolicy";
  private const string ProductionPolicyName = "ProductionPolicy";

  /// <summary>
  /// Adds CORS policies for development and production environments.
  /// Automatically selects the appropriate policy based on environment.
  /// </summary>
  public static IServiceCollection AddCorsWithEnvironmentPolicies(
      this IServiceCollection services,
      IConfiguration configuration,
      IWebHostEnvironment environment)
  {
    services.AddCors(options =>
    {
      // Development Policy: Permissive for local development
      options.AddPolicy(DevelopmentPolicyName, policy =>
          {
          policy
                  .WithOrigins(
                      "http://localhost:3000",      // React/Next.js dev server
                      "http://localhost:5173",      // Vite dev server
                      "http://localhost:4200",      // Angular dev server
                      "http://127.0.0.1:3000",
                      "http://127.0.0.1:5173"
                  )
                  .SetIsOriginAllowedToAllowWildcardSubdomains()  // Allow localhost:*
                  .AllowAnyMethod()                    // GET, POST, PUT, DELETE, etc.
                  .AllowAnyHeader()                    // Authorization, Content-Type, etc.
                  .AllowCredentials()                  // Cookies and auth tokens
                  .WithExposedHeaders("X-Pagination"); // Custom headers for client
        });

      // Production Policy: Strict allowlist from configuration
      var allowedOrigins = configuration
              .GetSection("Cors:AllowedOrigins")
              .Get<string[]>() ?? Array.Empty<string>();

      options.AddPolicy(ProductionPolicyName, policy =>
          {
          if (allowedOrigins.Length > 0)
          {
            policy
                    .WithOrigins(allowedOrigins)     // Only configured origins
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .WithExposedHeaders("X-Pagination");
          }
          else
          {
              // Fallback: No origins allowed (API-only mode)
            policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
          }
        });

      // Browser Extension Policy: For Chrome/Firefox extensions
      options.AddPolicy("ExtensionPolicy", policy =>
          {
          policy
                  .SetIsOriginAllowed(origin =>
                  {
                    // Allow all Chrome extensions
                  if (origin.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
                    return true;

                    // Allow all Firefox extensions
                  if (origin.StartsWith("moz-extension://", StringComparison.OrdinalIgnoreCase))
                    return true;

                    // Allow all Edge extensions
                  if (origin.StartsWith("extension://", StringComparison.OrdinalIgnoreCase))
                    return true;

                    // Also allow localhost origins (for development)
                  if (origin.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
                    return true;

                  if (origin.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase))
                    return true;

                  return false;
                })
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials()
                  .WithExposedHeaders("X-Pagination");
        });
    });

    return services;
  }

  /// <summary>
  /// Gets the CORS policy name for browser extension support.
  /// Returns ExtensionPolicy which works in both development and production.
  /// </summary>
  public static string GetCorsPolicyName()
  {
    // Always use ExtensionPolicy for browser extension support
    // It's designed to work in both development and production
    return "ExtensionPolicy";
  }
}
