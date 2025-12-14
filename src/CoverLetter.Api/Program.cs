using Asp.Versioning;
using CoverLetter.Api.Endpoints;
using CoverLetter.Api.Middleware;
using CoverLetter.Application;
using CoverLetter.Infrastructure;
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

// ========== OpenAPI / Swagger ==========
builder.Services.AddOpenApi();

var app = builder.Build();

// ========== Middleware Pipeline ==========
// Serilog request logging FIRST - logs the final response status
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
});

// Exception handler converts exceptions to proper HTTP responses
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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

app.Run();
