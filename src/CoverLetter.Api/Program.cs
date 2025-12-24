using Asp.Versioning;
using CoverLetter.Api.Endpoints;
using CoverLetter.Api.Extensions;
using CoverLetter.Api.Middleware;
using CoverLetter.Api.Services;
using CoverLetter.Application;
using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Infrastructure;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ========== Serilog Configuration ==========
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ========== Global Exception Handler ==========
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
// Serilog request logging FIRST - logs the final response status
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
});

// Exception handler converts exceptions to proper HTTP responses
app.UseExceptionHandler();

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
app.MapHealthEndpoints();

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

app.Run();
