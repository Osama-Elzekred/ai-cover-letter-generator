# Architecture Documentation

## Project Overview

AI-powered cover letter generation system consisting of:
- **Browser Extension** (Chrome/Edge) - Extracts job descriptions from LinkedIn, generates cover letters
- **.NET 10 Backend** - REST/gRPC APIs, Groq LLM integration, microservices architecture

## Current Implementation (Phase 1)

### Technology Stack
| Layer | Technology |
|-------|------------|
| Framework | .NET 10, C# 14 |
| API Style | Minimal APIs |
| CQRS | MediatR with pipeline behaviors |
| Validation | FluentValidation |
| HTTP Client | Refit |
| Logging | Serilog (structured, OpenTelemetry-ready) |
| LLM | Groq API (llama-3.3-70b-versatile) |

### Project Structure (Onion Architecture)
```
src/
├── CoverLetter.Domain/        # Entities, Result pattern, no dependencies
├── CoverLetter.Application/   # Use cases, MediatR handlers, behaviors
├── CoverLetter.Infrastructure/# External services (Groq via Refit)
└── CoverLetter.Api/           # Endpoints, middleware, DI setup
tests/
└── CoverLetter.Application.Tests/
```

### Request Flow
```
HTTP Request
    ↓
Serilog RequestLogging (logs final status)
    ↓
GlobalExceptionHandler (catches all exceptions)
    ↓
Endpoint (maps to MediatR command)
    ↓
ValidationBehavior (FluentValidation)
    ↓
LoggingBehavior (logs success/duration)
    ↓
Handler (business logic)
    ↓
Result<T> → ResultExtensions → IResult
    ↓
HTTP Response
```

### Error Handling Flow
```
┌─────────────────────────────────────────────────────────────┐
│ FluentValidation fails                                      │
│    → ValidationException                                    │
│    → GlobalExceptionHandler                                 │
│    → 400 + ValidationProblemDetails (field-level errors)    │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ Business logic fails                                        │
│    → Result.NotFound() / Result.ValidationError()           │
│    → ResultExtensions.ToHttpResult()                        │
│    → 4xx + ProblemDetails                                   │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ Unexpected exception                                        │
│    → GlobalExceptionHandler                                 │
│    → Logs at Error level with stack trace                   │
│    → 500 + ProblemDetails                                   │
└─────────────────────────────────────────────────────────────┘
```

---

## Future Architecture (Phases 2-7)

### Planned Services
1. **API Gateway** - REST, auth, rate limiting, routing
2. **Request Service** (gRPC) - Receives requests, stores to DB, writes to Outbox
3. **Generation Service** (REST) - Consumes queue, calls LLM, stores results
4. **Message Queue** - RabbitMQ or Kafka
5. **Background Workers** - Outbox processor

### Database Schema (PostgreSQL)
- Users
- CVs
- PromptTemplates
- CoverLetterRequests
- OutboxMessages
- CoverLetters

### Observability Stack
- OpenTelemetry (metrics + traces)
- Prometheus (scraping)
- Grafana (dashboards)
- Loki (logs)

### Deployment
- Docker containers
- Kubernetes (Kustomize or Helm)
- GitHub Actions CI/CD

---

## Browser Extension (Phase 2)

- Chrome Manifest v3
- Content script for LinkedIn job extraction
- Popup UI (React or vanilla JS)
- Chrome Storage for CV and settings
- Calls API Gateway

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Onion Architecture | Clear separation, testability, future microservices migration |
| MediatR | CQRS pattern, cross-cutting concerns via behaviors |
| Result<T> pattern | Explicit error handling without exceptions for business logic |
| Serilog | Structured logging, OpenTelemetry integration |
| Refit | Type-safe HTTP clients, cleaner than raw HttpClient |
| FluentValidation | Declarative validation, integrates with MediatR pipeline |

---

## Configuration

### User Secrets (Development)
```bash
dotnet user-secrets set "Groq:ApiKey" "your-api-key"
```

### appsettings.json Structure
```json
{
  "Groq": {
    "ApiKey": "",
    "BaseUrl": "https://api.groq.com/openai/v1",
    "Model": "llama-3.3-70b-versatile"
  },
  "Serilog": { ... }
}
```
