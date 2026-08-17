# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Purpose

This is an **educational project** — the goal is learning advanced backend patterns and tooling, not just shipping features. When making changes or suggestions:
- Explain the "why" behind decisions; include pros/cons and alternatives.
- Prefer production-grade patterns even if "overkill" for a project this size — that's the point.
- Connect concepts to real-world systems (e.g., Netflix/Uber-style patterns) where relevant.

The product itself: a Chrome extension + .NET backend that generates personalized cover letters and tailored CVs from a stored CV and a job description, auto-detects job context on LinkedIn, and answers LinkedIn application text-fields using the user's CV.

## Repo Layout

```
src/CoverLetter.Domain/         # Entities, Result<T>, enums — zero dependencies
src/CoverLetter.Application/    # MediatR use cases, behaviors, interfaces, repositories (abstractions)
src/CoverLetter.Infrastructure/ # EF Core/Postgres, Groq (Refit), RabbitMQ, LaTeX compiler, background services
src/CoverLetter.Api/            # Minimal API endpoints, middleware, DI wiring, Program.cs
tests/CoverLetter.Application.Tests/  # xUnit + NSubstitute + FluentAssertions, mirrors Application structure
apps/extension/                # Chrome MV3 extension (TypeScript, no framework), builds to apps/extension/dist
infrastructure/observability/  # Grafana/Prometheus/Loki provisioning
docs/                          # ARCHITECTURE.md, FOLDER-STRUCTURE-EXPLAINED.md, PROJECT-ROADMAP.md, Latex-Compiler-Queue-Worker-Plan.md
```

Onion/Clean Architecture dependency rule: `Domain ← Application ← Infrastructure`, and `Api` depends on `Application` + `Infrastructure`. Inner layers never reference outer layers. Application defines interfaces (`Common/Interfaces/`), Infrastructure implements them, DI wiring happens in each layer's `DependencyInjection.cs`.

**Note:** `docs/ARCHITECTURE.md` and `docs/PROJECT-ROADMAP.md` describe the queue/outbox/RabbitMQ pipeline as a "future phase" — it is already implemented (see below). Trust the code over those docs for current state; the roadmap files are historical/aspirational, not a live status board.

## Build, Run, Test

Default working directory for API commands: `src/CoverLetter.Api`.

```bash
# Solution-wide build (from repo root)
dotnet build AiCoverLetter.slnx

# Run API with hot reload (from src/CoverLetter.Api)
dotnet watch run

# Run all tests (from repo root or tests/CoverLetter.Application.Tests)
dotnet test

# Run a single test
dotnet test --filter "FullyQualifiedName~GenerateCoverLetterHandlerTests"

# EF Core migrations (from src/CoverLetter.Api, targeting Infrastructure)
dotnet ef database update
dotnet ef migrations add <Name> --project ../CoverLetter.Infrastructure --startup-project .
```

Local dev services (Postgres, Grafana, Prometheus, Loki, RabbitMQ, LaTeX compiler) run via `docker-compose.dev.yml`. Typical flow: `bash dev-services.sh` (starts infra) in one terminal, `dotnet watch run` in `src/CoverLetter.Api` in another. Full containerized stack: `docker-compose -f docker-compose.dev.yml up`.

- API: http://localhost:5012, docs at `/scalar/v1` (Development only)
- Groq API key: set via `dotnet user-secrets set "Groq:ApiKey" "..."` locally, or `GROQ_API_KEY` env var / `.env` for Docker

### Extension (`apps/extension`)

```bash
cd apps/extension
npm run build        # dev build → dist/
npm run build:prod    # production build (skips npm install, sets prod logging)
npm run watch          # tsc --watch
npm run type-check     # tsc --noEmit
```

`build.js` generates `src/config/build-env.ts` (build-mode/logging flags) before compiling — don't hand-edit that generated file. Load `dist/` as an unpacked extension in Chrome; host permission is scoped to `localhost:5012` and `linkedin.com`.

## Request & Error Flow

```
Serilog request logging → GlobalExceptionHandler → Endpoint → MediatR
  → ValidationBehavior (FluentValidation) → LoggingBehavior → Handler
  → Result<T> → ResultExtensions.ToHttpResult() → HTTP response
```

- `GlobalExceptionHandler` (`src/CoverLetter.Api/Middleware/GlobalExceptionHandler.cs`) catches all unhandled exceptions and returns RFC-compliant `ProblemDetails`. **Do not add try/catch in handlers/endpoints for this** — rely on the global handler.
- Business failures use `Result<T>` (`src/CoverLetter.Domain/Common/Result.cs`) with a `ResultType` (NotFound, ValidationError, Unauthorized, Forbidden, Conflict, TooManyRequests, ...). `ResultExtensions.ToHttpResult()` (`src/CoverLetter.Api/Extensions/ResultExtensions.cs`, uses C# 14 extension members) maps each type to the right HTTP status + `ProblemDetails`.
- FluentValidation failures throw `ValidationException`, caught by the same global handler → 400 with field-level errors.
- Never use `.Result` or `.Wait()` on tasks.

## Compile Pipeline (Outbox/Inbox + RabbitMQ)

CV customization and raw LaTeX compilation are **async, queue-backed**, not synchronous Docker calls from the request thread:

1. Endpoint creates a `CompileJob` row + an `OutboxMessage` row in one DB transaction, returns `202 Accepted` with a `jobId`.
2. `OutboxDispatcherBackgroundService` polls unsent outbox rows and publishes to RabbitMQ.
3. `CompileJobConsumerBackgroundService` consumes, runs the LaTeX compile, writes to `ICompileResultStorage` (file volume), updates job status. `InboxProcessed` table dedupes at-least-once delivery.
4. Client polls `GET /cv/compile/status/{jobId}`, then downloads via `GET /cv/compile/result/{jobId}`.

Both background services are hosted **inside the API process** (registered in `CoverLetter.Infrastructure/DependencyInjection.cs`), not a separate worker service — see `docs/Latex-Compiler-Queue-Worker-Plan.md` for the fuller design rationale (that doc's "next steps" section is largely done; the standalone worker project it mentions was never split out).

Relevant config section in `appsettings.json`: `RabbitMq` (exchange/queue/DLX/DLQ) and `CompileWorker` (concurrency, timeout, storage path, outbox poll interval/batch/backoff).

## Cross-Cutting Concerns

- **User identity**: anonymous `X-User-Id` header, extracted by `UserContextMiddleware` into `IUserContext` (`src/CoverLetter.Api/Services/UserContext.cs`). Required on all requests (documented in the OpenAPI info block).
- **Idempotency**: expensive POST endpoints (cover letter/CV generation) accept `X-Idempotency-Key`, enforced via `IdempotencyBehavior` in the MediatR pipeline.
- **Rate limiting (BYOK)**: policy `"ByokPolicy"` (`src/CoverLetter.Api/Extensions/RateLimitingExtensions.cs`) — users with a saved API key get `GetNoLimiter()` (unlimited); anonymous users get a 10 req/min sliding-window limiter per IP (6 segments, queue limit 2). Endpoints opt in explicitly via `.RequireRateLimiting("ByokPolicy")`; never apply it to health/settings/docs endpoints.
- **LLM provider**: Groq (`llama-3.3-70b-versatile`) via Refit (`IGroqApi`), wrapped by `LoggingLlmService` — a decorator that logs full prompt/response at `Debug` level for the `CoverLetter.Infrastructure.LlmProviders` namespace only. Toggle at runtime without restart via `PUT /api/v1/debug/llm-log-level` (Development only). Adding another provider means a new `LlmProviders/<Provider>/` folder implementing `ILlmService`.
- **Custom prompts**: per-user, per-service (`cv-customization`, `cover-letter`, `match-analysis`, `textarea-answer`), stored via `IUserPromptRepository`, falling back to defaults from `PromptRegistry`/`IPromptRegistry` when unset.

## Logging Rules

- Log once, at the boundary: Serilog request logging + `GlobalExceptionHandler`. Don't duplicate logging inside handlers or Infrastructure services.
- Expected/validation errors log at `Debug`; unexpected exceptions log at `Error` with stack trace.
- Structured logs ship to Loki (Grafana at http://localhost:3000, admin/admin); metrics scraped by Prometheus via `prometheus-net.AspNetCore` at `/metrics`.

## Endpoints & Versioning

- All feature endpoints are minimal APIs (not MVC controllers), one static class per feature under `src/CoverLetter.Api/Endpoints/`, each with a `Map<Feature>Endpoints` extension method called from `Program.cs`.
- Routes are versioned via URL segment and grouped under `/api/v{version}` (currently only v1) — see `Program.cs`. New endpoints go through the `v1Routes` group already set up there, not `app.Map...` directly.
- Endpoint → MediatR command/query → `Result<T>` → `.ToHttpResult()`. Keep endpoint handler methods thin; business logic belongs in the Application-layer handler.

## Conventions When Adding a Use Case

Follow the existing vertical-slice layout under `src/CoverLetter.Application/UseCases/<Feature>/`: `<Feature>Command.cs` (or `Query.cs`), `<Feature>Handler.cs`, `<Feature>Validator.cs` (FluentValidation), `<Feature>Result.cs`. Register any new external dependency's interface in `Application/Common/Interfaces/`, implement it in Infrastructure, and wire it in `CoverLetter.Infrastructure/DependencyInjection.cs`.

## DO NOT

- Duplicate logging across layers.
- Log validation/expected client errors at `Error` level.
- Add try/catch where `GlobalExceptionHandler` already handles it.
- Put business logic in `Api`-layer endpoint methods — delegate to MediatR handlers.
- Reference Infrastructure types from Application — depend on the interface, not the concrete implementation.
