using System.Net.Http.Headers;
using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Infrastructure.CvParsers;
using CoverLetter.Infrastructure.LlmProviders.Groq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refit;

namespace CoverLetter.Infrastructure;

/// <summary>
/// Dependency injection extensions for Infrastructure layer.
/// </summary>
public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructure(
      this IServiceCollection services,
      IConfiguration configuration)
  {
    // Bind Groq settings
    services.Configure<GroqSettings>(
        configuration.GetSection(GroqSettings.SectionName));

    // Register Refit client for Groq API
    services.AddRefitClient<IGroqApi>()
        .ConfigureHttpClient((sp, client) =>
        {
          var settings = sp.GetRequiredService<IOptions<GroqSettings>>().Value;
          client.BaseAddress = new Uri(settings.BaseUrl);
          client.DefaultRequestHeaders.Authorization =
                  new AuthenticationHeaderValue("Bearer", settings.ApiKey);
          client.DefaultRequestHeaders.Accept.Add(
                  new MediaTypeWithQualityHeaderValue("application/json"));
          client.Timeout = TimeSpan.FromSeconds(120);
        });

    // Register LLM service
    services.AddScoped<ILlmService, GroqLlmService>();

    // Register CV parser service
    services.AddScoped<ICvParserService, CvParserService>();

    return services;
  }
}
