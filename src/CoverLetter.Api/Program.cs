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
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Serilog request logging (replaces default HTTP logging)
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
});

app.UseHttpsRedirection();

// ========== Endpoints ==========
app.MapHealthEndpoints();
app.MapCoverLetterEndpoints();

app.Run();
