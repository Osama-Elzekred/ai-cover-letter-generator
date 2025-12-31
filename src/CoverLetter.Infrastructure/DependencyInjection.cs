using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Infrastructure.CvParsers;
using CoverLetter.Infrastructure.LlmProviders.Groq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

    // Register HttpClientFactory for dynamic Groq API clients (BYOK support)
    services.AddHttpClient("GroqClient");

    // Register LLM service
    services.AddScoped<ILlmService, GroqLlmService>();

    // Register CV parser service
    services.AddScoped<ICvParserService, CvParserService>();

    // Register CV repository
    services.AddScoped<ICvRepository, CoverLetter.Infrastructure.Repositories.CvRepository>();

    // Register LaTeX compiler service
    services.AddScoped<ILatexCompilerService, CoverLetter.Infrastructure.Services.LatexCompilerService>();

    return services;
  }
}
