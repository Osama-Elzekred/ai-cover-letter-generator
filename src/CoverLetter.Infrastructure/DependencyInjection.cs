using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Repositories;
using CoverLetter.Infrastructure.CvParsers;
using CoverLetter.Infrastructure.LlmProviders.Groq;
using CoverLetter.Infrastructure.Persistence;
using CoverLetter.Infrastructure.Repositories;
using CoverLetter.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
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

    // Register PostgreSQL with EF Core + connection resiliency
    var connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    services.AddDbContext<AppDbContext>(options =>
    {
      options.UseNpgsql(
          connectionString,
          npgsqlOptions =>
          {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
            npgsqlOptions.CommandTimeout(30);
          });
    });

    // Register LLM service
    services.AddScoped<ILlmService, GroqLlmService>();

    // Register CV parser service
    services.AddScoped<ICvParserService, CvParserService>();

    // Register query context (Queries use IQueryable directly)
    services.AddScoped<IQueryContext>(sp => sp.GetRequiredService<AppDbContext>());

    // Register Unit of Work (Commands call SaveChangesAsync)
    services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

    // Register DB-backed repositories (Commands: write operations, aggregates)
    services.AddScoped<ICvRepository, DbCvRepository>();
    services.AddScoped<ICoverLetterRepository, CoverLetterRepository>();
    services.AddScoped<IIdempotencyKeyRepository, DbIdempotencyKeyRepository>();
    services.AddScoped<IUserPromptRepository, DbUserPromptRepository>();

    // Register LaTeX compiler service
    services.AddScoped<ILatexCompilerService, LatexCompilerService>();

    return services;
  }
}
