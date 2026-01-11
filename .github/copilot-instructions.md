# Copilot Instructions

## Project Purpose
This is an educational project — the goal is learning advanced backend patterns and tooling.
- Explain the "why" behind decisions; include pros/cons and alternatives
- Prefer production-grade patterns even if "overkill" for learning value
- Connect concepts to real-world systems (e.g., Netflix/Uber patterns)

## Big-Picture Architecture
- Onion layers: Domain → Application → Infrastructure → Api. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and [docs/FOLDER-STRUCTURE-EXPLAINED.md](docs/FOLDER-STRUCTURE-EXPLAINED.md).
- Minimal APIs with MediatR and pipeline behaviors; `Result<T>` drives HTTP via extension members in [src/CoverLetter.Api/Extensions/ResultExtensions.cs](src/CoverLetter.Api/Extensions/ResultExtensions.cs).
- External integrations (Groq LLM, CV parsing, LaTeX) live in Infrastructure with DI registered in [src/CoverLetter.Infrastructure/DependencyInjection.cs](src/CoverLetter.Infrastructure/DependencyInjection.cs).
- Versioned routes via URL segments; v1 grouped under `/api/v{version}` in [src/CoverLetter.Api/Program.cs](src/CoverLetter.Api/Program.cs).

## Request & Error Flow
- Flow: Serilog request logging → GlobalExceptionHandler → Endpoint → MediatR → Behaviors (validation/logging) → Handler → `Result<T>` → HTTP.
- GlobalExceptionHandler returns RFC-compliant `ProblemDetails` and logs at correct levels. See [src/CoverLetter.Api/Middleware/GlobalExceptionHandler.cs](src/CoverLetter.Api/Middleware/GlobalExceptionHandler.cs).
- Business failures use `Result<T>` statuses mapped in ResultExtensions. Validation uses FluentValidation in Application.

## Critical Headers & Idempotency
- All requests support anonymous `X-User-Id` via [src/CoverLetter.Api/Middleware/UserContextMiddleware.cs](src/CoverLetter.Api/Middleware/UserContextMiddleware.cs) and [src/CoverLetter.Api/Services/UserContext.cs](src/CoverLetter.Api/Services/UserContext.cs).
- Expensive POST endpoints accept `X-Idempotency-Key` and explicitly require rate limiting policy.
- Example endpoints/patterns in [src/CoverLetter.Api/Endpoints/CoverLetterEndpoints.cs](src/CoverLetter.Api/Endpoints/CoverLetterEndpoints.cs).

## Rate Limiting (BYOK)
- Policy "ByokPolicy" in [src/CoverLetter.Api/Extensions/RateLimitingExtensions.cs](src/CoverLetter.Api/Extensions/RateLimitingExtensions.cs):
  - Users with saved API key (BYOK) → unlimited (no limiter)
  - Without key → sliding window 10 req/min per IP, queue limit 2
- Endpoints opt-in via `.RequireRateLimiting("ByokPolicy")`; do not apply to health/settings/docs.

## Build, Run, Test
- Default working directory: `src/CoverLetter.Api`.
- Use full-path commands:
  - `cd "d:/Projects/Cover Letter Generator/ai-cover-letter-generator/src/CoverLetter.Api" && dotnet run`
  - `cd "d:/Projects/Cover Letter Generator/ai-cover-letter-generator/src/CoverLetter.Api" && dotnet build`
  - `cd "d:/Projects/Cover Letter Generator/ai-cover-letter-generator/src/CoverLetter.Api" && dotnet test`
- Solution-wide build: `cd "d:/Projects/Cover Letter Generator/ai-cover-letter-generator" && dotnet build`.
- VS Code tasks available: build/publish/watch at the workspace root.

## Configuration & Secrets
- Groq settings section `Groq` bound in Infrastructure; set user-secret `Groq:ApiKey` for development.
- App config: see [src/CoverLetter.Api/appsettings.json](src/CoverLetter.Api/appsettings.json) and [src/CoverLetter.Infrastructure/LlmProviders/Groq/GroqSettings.cs](src/CoverLetter.Infrastructure/LlmProviders/Groq/GroqSettings.cs) for `BaseUrl`, `Model`, `Temperature`, `MaxTokens`.
- Scalar docs served in development at `/scalar/v1` as configured in [src/CoverLetter.Api/Program.cs](src/CoverLetter.Api/Program.cs).

## Endpoints & Patterns
- Map versioned groups in `Program.cs`; feature endpoints organized per file under [src/CoverLetter.Api/Endpoints](src/CoverLetter.Api/Endpoints).
- Endpoint → MediatR command → `Result<T>` → `ToHttpResult()`; avoid try/catch, rely on global handler.
- For rate-limited features, include `.RequireRateLimiting("ByokPolicy")` and accept idempotency header.

## Logging
- Log once at boundaries: Serilog request logging and GlobalExceptionHandler.
- Validation and expected client errors at Debug; unexpected errors at Error.
- Avoid duplicate logs in handlers or infrastructure.

## Middleware Order
```csharp
app.UseSerilogRequestLogging(); // FIRST — logs final status
app.UseExceptionHandler();      // Converts exceptions to ProblemDetails
app.UseCors(...);
app.UseMiddleware<UserContextMiddleware>();
app.UseRateLimiter();
```

## Error Handling
- FluentValidation → `ValidationException` → global handler → 400
- Business errors → `Result<T>` → [ResultExtensions](src/CoverLetter.Api/Extensions/ResultExtensions.cs) → appropriate 4xx/409
- Unexpected → global handler → 5xx with `traceId`

## Docs & HTTP Tests
- HTTP test files in [src/CoverLetter.Api/http-tests](src/CoverLetter.Api/http-tests); example requests for cover letters/CV/settings/health.
- API docs: open [src/CoverLetter.Api/wwwroot/scalar](src/CoverLetter.Api/wwwroot/scalar); served at `/scalar/v1` in Development.

## DO NOT
- Duplicate logging across layers
- Log validation at Error level
- Add try-catch where GlobalExceptionHandler handles it
- Suggest code without checking existing patterns
- Use `.Result` or `.Wait()`

Feedback: If any section feels unclear or incomplete (e.g., secrets naming vs environment variables, or extension/API contract details), tell me which part and I’ll refine it.

