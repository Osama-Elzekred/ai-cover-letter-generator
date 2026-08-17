using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Repositories;
using CoverLetter.Infrastructure.BackgroundServices;
using CoverLetter.Infrastructure.Configuration;
using CoverLetter.Infrastructure.CvParsers;
using CoverLetter.Infrastructure.LlmProviders;
using CoverLetter.Infrastructure.LlmProviders.Groq;
using CoverLetter.Infrastructure.Messaging;
using CoverLetter.Infrastructure.Persistence;
using CoverLetter.Infrastructure.Repositories;
using CoverLetter.Infrastructure.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

        // Bind compile pipeline settings
        services.Configure<RabbitMqSettings>(
            configuration.GetSection(RabbitMqSettings.SectionName));
        services.Configure<CompileWorkerSettings>(
            configuration.GetSection(CompileWorkerSettings.SectionName));
        // Expose the bound CompileWorkerSettings instance for direct constructor injection
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<CompileWorkerSettings>>().Value);

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

        // Register LLM service with a logging decorator.
        // The decorator wraps the real GroqLlmService and logs the full prompt + response
        // at Debug level — covering every LLM caller automatically without touching handlers.
        // Toggle visibility via log level: Debug = see prompts, Info = silent.
        services.AddScoped<GroqLlmService>();
        services.AddScoped<ILlmService>(sp => new LoggingLlmService(
            sp.GetRequiredService<GroqLlmService>(),
            sp.GetRequiredService<ILogger<LoggingLlmService>>()));

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
        services.AddScoped<IUserApiKeyRepository, DbUserApiKeyRepository>();

        // Compile pipeline repositories
        services.AddScoped<ICompileJobRepository, DbCompileJobRepository>();
        services.AddScoped<IOutboxMessageRepository, DbOutboxMessageRepository>();
        services.AddScoped<IInboxProcessedRepository, DbInboxProcessedRepository>();

        // Register LaTeX compiler service (used by the consumer)
        services.AddScoped<ILatexCompilerService, LatexCompilerService>();

        // Compile result storage (file-backed volume)
        services.AddScoped<ICompileResultStorage, FileCompileResultStorage>();

        // MassTransit bus + RabbitMQ transport for compile job delivery
        services.AddMassTransit(x =>
        {
            x.AddConsumer<CompileJobConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitMqSettings = context.GetRequiredService<IOptions<RabbitMqSettings>>().Value;
                var workerSettings = context.GetRequiredService<IOptions<CompileWorkerSettings>>().Value;
                var rabbitMqHost = new Uri($"rabbitmq://{rabbitMqSettings.Host}:{rabbitMqSettings.Port}{rabbitMqSettings.VirtualHost}");

                cfg.Host(rabbitMqHost, h =>
                {
                    h.Username(rabbitMqSettings.UserName);
                    h.Password(rabbitMqSettings.Password);
                });

                cfg.Message<CompileJobMessage>(m => m.SetEntityName(rabbitMqSettings.Exchange));

                cfg.ReceiveEndpoint(rabbitMqSettings.Queue, e =>
                {
                    e.ConfigureConsumer<CompileJobConsumer>(context);
                    e.PrefetchCount = (ushort)Math.Min(workerSettings.MaxConcurrency, ushort.MaxValue);
                    e.UseMessageRetry(r => r.Exponential(
                        workerSettings.MassTransitRetryAttempts,
                        TimeSpan.FromSeconds(workerSettings.MassTransitRetryMinSeconds),
                        TimeSpan.FromSeconds(workerSettings.MassTransitRetryMaxSeconds),
                        TimeSpan.FromSeconds(workerSettings.MassTransitRetryIntervalSeconds)));
                });
            });
        });

        services.AddSingleton<ICompileMessagePublisher, MassTransitCompileMessagePublisher>();

        // Compile pipeline background services (hosted inside the API process)
        services.AddHostedService<OutboxDispatcherBackgroundService>();

        return services;
    }
}
