using CoverLetter.Api.Configuration;
using CoverLetter.Api.Models;
using CoverLetter.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ========== Configuration ==========
builder.Services.Configure<GroqSettings>(
    builder.Configuration.GetSection(GroqSettings.SectionName));

// ========== Services ==========
builder.Services.AddHttpClient<IGroqChatClient, GroqChatClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddScoped<ICoverLetterService, CoverLetterService>();

// ========== OpenAPI / Swagger ==========
builder.Services.AddOpenApi();

var app = builder.Build();

// ========== Middleware Pipeline ==========
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// ========== Endpoints ==========

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("Health");

// Generate cover letter endpoint
app.MapPost("/generate-cover-letter", async (
    GenerateCoverLetterRequest request,
    ICoverLetterService coverLetterService,
    CancellationToken cancellationToken) =>
{
    // Validate request
    var validationErrors = request.Validate().ToList();
    if (validationErrors.Count > 0)
    {
        return Results.BadRequest(new
        {
            Title = "Validation Failed",
            Errors = validationErrors
        });
    }

    try
    {
        var response = await coverLetterService.GenerateAsync(request, cancellationToken);
        return Results.Ok(response);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem(
            title: "LLM Service Error",
            detail: ex.Message,
            statusCode: StatusCodes.Status502BadGateway);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(
            title: "Generation Failed",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("GenerateCoverLetter")
.WithTags("Cover Letter")
.Produces<GenerateCoverLetterResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status502BadGateway);

app.Run();
