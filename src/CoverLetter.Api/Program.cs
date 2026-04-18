using Asp.Versioning;
using CoverLetter.Api.Configuration;
using CoverLetter.Api.Endpoints;
using CoverLetter.Api.Extensions;
using CoverLetter.Api.HealthChecks;
using CoverLetter.Api.Logging;
using CoverLetter.Api.Middleware;
using CoverLetter.Api.Services;
using CoverLetter.Application;
using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Infrastructure;
using CoverLetter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Prometheus;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;


var builder = WebApplication.CreateBuilder(args);

// ========== Observability Configuration ==========
// Load observability settings from appsettings.json
var observabilityConfig = builder.Configuration.GetSection("Observability");
var observabilitySettings = new ObservabilitySettings
{
    SlowRequestThresholdMs = observabilityConfig.GetValue<int>("SlowRequestThresholdMs", 100),
    LogTimeZone = observabilityConfig.GetValue<string>("LogTimeZone") ?? "UTC",
    LogTimeZoneOffset = observabilityConfig.GetValue<string>("LogTimeZoneOffset") ?? "+00:00"
};
builder.Services.AddSingleton(observabilitySettings);

// ========== LLM Log-Level Switch ==========
// Singleton that controls the minimum log level for the LLM provider namespace at runtime.
// Starts OFF (Information) in all environments — no prompt/response logging by default.
// Toggle via PUT /api/v1/debug/llm-log-level — no restart needed.
var llmLogLevelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
builder.Services.AddSingleton(llmLogLevelSwitch);

// ========== Serilog Configuration ==========
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .MinimumLevel.Override("CoverLetter.Infrastructure.LlmProviders", llmLogLevelSwitch)
    .Enrich.FromLogContext()
    .Enrich.With(new TimestampEnricher(observabilitySettings))
    .Enrich.With(new FormattedLogEnricher(observabilitySettings))
    .WriteTo.GrafanaLoki(
        // uri: "http://host.docker.internal:3100", /  / Use host.docker.internal on Windows for Docker networking
        uri: "http://localhost:3100",  // change it later to host.docker.internal when app running in Docker 
                                                  //   // Use host.docker.internal on Windows for Docker networking
        labels: new[]
        {
            new LokiLabel { Key = "app", Value = "coverletter-api" },
            new LokiLabel { Key = "environment", Value = builder.Environment.EnvironmentName },
            new LokiLabel { Key = "version", Value = "1.0.0" },
            new LokiLabel { Key = "host", Value = Environment.MachineName }
        },
        credentials: null
    ));

// ========== Prometheus Metrics (using prometheus-net) ==========
// prometheus-net.AspNetCore automatically collects ASP.NET Core and HTTP metrics
// Exposed at /metrics endpoint for Prometheus to scrape

// Global exception handler converts exceptions to proper HTTP responses
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ========== API Versioning ==========
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// ========== Layer Registration ==========
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ========== User Context Service ==========
builder.Services.AddHttpContextAccessor();  // Required for UserContext
builder.Services.AddScoped<IUserContext, UserContext>();

// ========== CORS Configuration ==========
builder.Services.AddCorsWithEnvironmentPolicies(builder.Configuration, builder.Environment);

// ========== Rate Limiting ==========
builder.Services.AddRateLimitingWithByok();

// ========== Health Checks ==========
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "dependency" })
    .AddCheck<MemoryCacheHealthCheck>("memory_cache", tags: new[] { "dependency" })
    .AddCheck<LatexCompilerHealthCheck>("latex_compiler", tags: new[] { "dependency" });

var healthCheckOptions = new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("dependency"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
    }
};

// ========== OpenAPI / Swagger ==========
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "AI Cover Letter Generator",
            Version = "v1",
            Description = """
                Generate personalized cover letters & Custom CVs using AI.
                
                **Required Header:** All requests must include `X-User-Id` header (e.g., `X-User-Id: test-user-123`)
                """,
            Contact = new()
            {
                Name = "API Support",
                Url = new Uri("https://github.com/Osama-Elzekred/ai-cover-letter-generator")
            },
            License = new()
            {
                Name = "MIT",
                Url = new Uri("https://opensource.org/licenses/MIT")
            }
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// ========== Middleware Pipeline ==========
// HTTP request logging - logs all requests with appropriate log levels
app.UseMiddleware<HttpRequestLoggingMiddleware>();

// Prometheus HTTP metrics collection middleware
app.UseHttpMetrics();

// Exception handler converts exceptions to proper HTTP responses
app.UseExceptionHandler();

// Request latency and slow request tracking is handled via structured logs in Loki
// (RequestPath, Elapsed, and StatusCode are enriched and queryable)

// CORS must be before authentication and authorization
app.UseCors(CorsExtensions.GetCorsPolicyName());

// User context extraction (X-User-Id header → HttpContext.Items)
app.UseMiddleware<UserContextMiddleware>();

// Rate limiting (must be after UserContextMiddleware to access IUserContext)
app.UseRateLimiter();

// ========== API Documentation & Static Files ========== 
app.UseStaticFiles(); // Enable serving files from wwwroot

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();  // Exposes OpenAPI JSON at /openapi/v1.json

    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("AI Cover Letter Generator")
            .WithTheme(ScalarTheme.Kepler)
            .WithDefaultHttpClient(ScalarTarget.JavaScript, ScalarClient.Fetch)
            .WithJavaScriptConfiguration("/scalar/config.js"); // Load custom JS config
    });
}

app.UseHttpsRedirection();

// ========== Endpoints ==========
app.MapHealthEndpoints(healthCheckOptions);

// Prometheus metrics endpoint - exposes metrics in Prometheus format
app.MapMetrics();

var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

// Version 1.0 routes
var v1Routes = app.MapGroup("/api/v{version:apiVersion}")
    .WithApiVersionSet(versionSet)
    .HasApiVersion(1.0);

v1Routes.MapCoverLetterEndpoints();
v1Routes.MapCvEndpoints();
v1Routes.MapSettingsEndpoints();
v1Routes.MapPromptsEndpoints();
v1Routes.MapTextareaAnswerEndpoints();

if (app.Environment.IsDevelopment())
    v1Routes.MapDebugEndpoints();

// ========== Startup Information ==========
if (app.Environment.IsDevelopment())
{
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStarted.Register(() =>
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ?? Array.Empty<string>();

        foreach (var address in addresses)
        {
            Log.Information("📚 API Docs: {DocsUrl}", $"{address}/scalar/v1");
        }
    });
}

// ========== Database Migrations ==========
await ApplyMigrations(app);

app.Run();

// ========== Helper Functions ==========
static async Task ApplyMigrations(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // await dbContext.Database.EnsureCreatedAsync();

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            app.Logger.LogInformation("Applying {count} pending migrations...", pendingMigrations.Count());
            await dbContext.Database.MigrateAsync();
            app.Logger.LogInformation("Migrations applied successfully.");
        }
        else
        {
            app.Logger.LogInformation("No pending migrations found.");
        }

    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred while applying migrations");
    }
}
