# Project Folder Structure - Clean/Onion Architecture Explained

## Overview: The Onion Layers

```
┌─────────────────────────────────────────────────────────┐
│                    CoverLetter.Api                      │  ← Outermost Layer
│  ┌───────────────────────────────────────────────────┐  │
│  │           CoverLetter.Infrastructure              │  │  ← External Services
│  │  ┌─────────────────────────────────────────────┐  │  │
│  │  │         CoverLetter.Application             │  │  │  ← Business Logic
│  │  │  ┌───────────────────────────────────────┐  │  │  │
│  │  │  │      CoverLetter.Domain               │  │  │  │  ← Core (Center)
│  │  │  │   - Entities                          │  │  │  │
│  │  │  │   - Result<T>                         │  │  │  │
│  │  │  └───────────────────────────────────────┘  │  │  │
│  │  │   - Use Cases (Handlers)                    │  │  │
│  │  │   - Interfaces (ILlmService)                │  │  │
│  │  └─────────────────────────────────────────────┘  │  │
│  │   - Groq Implementation                           │  │
│  └───────────────────────────────────────────────────┘  │
│   - Endpoints, Middleware                               │
└─────────────────────────────────────────────────────────┘

Dependency Rule: Outer layers depend on inner layers, NEVER the reverse
```

---

## Root Structure

```
ai-cover-letter-generator/
├── src/                          ← All source code
├── tests/                        ← Unit/integration tests
├── docs/                         ← Documentation
├── .github/                      ← GitHub workflows, copilot instructions
├── AiCoverLetter.sln             ← Solution file (groups all projects)
├── README.md                     ← Project overview
└── PROJECT-ROADMAP.md            ← Development roadmap
```

**Why this way?**
- Clear separation: source vs tests vs docs
- Solution file at root: Easy to open entire project
- Standard .NET convention

---

## Layer 1: Domain (The Core) 🎯

### Location: `src/CoverLetter.Domain/`

```
CoverLetter.Domain/
├── Common/
│   └── Result.cs                 ← Result<T> pattern
├── Entities/
│   └── CoverLetter.cs            ← Domain entity (if you had one)
└── CoverLetter.Domain.csproj     ← No external dependencies!
```

### Purpose
**The heart of your application** - business rules, domain entities, core types.

### Rules
- ❌ **NO dependencies** on other projects (no Application, Infrastructure, Api)
- ❌ **NO dependencies** on frameworks (no EF Core, no ASP.NET, no MediatR)
- ✅ **Only C# language** features and pure logic
- ✅ **Domain entities** (business objects like `CoverLetter`)
- ✅ **Value objects** (like `Result<T>`)
- ✅ **Domain exceptions**

### Why `Common/` folder?
Holds shared types used across the domain (Result pattern, enums like `ResultType`).

### Why `Entities/` folder?
Domain entities represent your business concepts. In DDD (Domain-Driven Design), these are rich objects with behavior.

**Real-world example:**
```csharp
// Domain/Entities/CoverLetter.cs
public class CoverLetter
{
    public string Content { get; private set; }
    public DateTime CreatedAt { get; private set; }
    
    // Business rule in domain
    public Result Publish()
    {
        if (string.IsNullOrWhiteSpace(Content))
            return Result.Failure("Cannot publish empty cover letter");
            
        IsPublished = true;
        return Result.Success();
    }
}
```

---

## Layer 2: Application (Business Logic) 🧠

### Location: `src/CoverLetter.Application/`

```
CoverLetter.Application/
├── Common/
│   ├── Behaviors/                ← MediatR pipeline behaviors
│   │   ├── LoggingBehavior.cs
│   │   ├── ValidationBehavior.cs
│   │   └── IdempotencyBehavior.cs
│   └── Interfaces/               ← Abstractions (Dependency Inversion)
│       └── ILlmService.cs        ← Interface (implemented in Infrastructure)
├── UseCases/
│   └── GenerateCoverLetter/      ← Feature folder (Vertical Slice)
│       ├── GenerateCoverLetterCommand.cs    ← Request
│       ├── GenerateCoverLetterHandler.cs    ← Business logic
│       ├── GenerateCoverLetterValidator.cs  ← Validation rules
│       └── GenerateCoverLetterResult.cs     ← Response
├── DependencyInjection.cs        ← Registers Application services
└── CoverLetter.Application.csproj
    Dependencies: Domain only!
```

### Purpose
**Orchestrates business operations** - use cases, application logic, validation.

### Rules
- ✅ **Depends on Domain** (can use `Result<T>`, entities)
- ❌ **Does NOT depend on Infrastructure or Api**
- ✅ **Defines interfaces** that Infrastructure implements (`ILlmService`)
- ✅ **Framework-agnostic business logic**

### Why `Common/Behaviors/`?
**Cross-cutting concerns** that apply to all use cases:
- Logging every request
- Validating every request
- Idempotency for some requests

**Alternative to AOP (Aspect-Oriented Programming)** - behaviors wrap handlers like middleware.

### Why `Common/Interfaces/`?
**Dependency Inversion Principle (DIP):**
```
Application defines:  ILlmService (interface)
Infrastructure implements: GroqLlmService (concrete class)

Application → Interface ← Infrastructure
(High-level)           (Low-level)
```

Application says "I need an LLM service" but doesn't care if it's Groq, OpenAI, or Claude.

### Why `UseCases/GenerateCoverLetter/`?
**Vertical Slice Architecture** - everything for one feature in one folder:
- Command (request)
- Handler (logic)
- Validator (rules)
- Result (response)

**Benefits:**
- ✅ Easy to find all related files
- ✅ Easy to add new features (just create new folder)
- ✅ Cohesive - high coupling within feature, low coupling between features

**Alternative (Traditional Layered):**
```
❌ BAD - Scattered across layers:
Application/
├── Commands/
│   └── GenerateCoverLetterCommand.cs
├── Handlers/
│   └── GenerateCoverLetterHandler.cs
├── Validators/
│   └── GenerateCoverLetterValidator.cs
└── Results/
    └── GenerateCoverLetterResult.cs

Hard to navigate, files far apart
```

---

## Layer 3: Infrastructure (External Services) 🔌

### Location: `src/CoverLetter.Infrastructure/`

```
CoverLetter.Infrastructure/
├── LlmProviders/
│   └── Groq/                     ← Provider-specific implementation
│       ├── GroqLlmService.cs     ← Implements ILlmService
│       ├── IGroqApi.cs           ← Refit interface
│       ├── GroqModels.cs         ← API request/response models
│       └── GroqSettings.cs       ← Configuration
├── DependencyInjection.cs        ← Registers Infrastructure services
└── CoverLetter.Infrastructure.csproj
    Dependencies: Application, Domain
```

### Purpose
**Implements external service integrations** - databases, APIs, file systems, email, etc.

### Rules
- ✅ **Depends on Application** (implements interfaces defined there)
- ✅ **Depends on Domain** (uses entities, Result<T>)
- ✅ **External dependencies OK** (Refit, EF Core, HttpClient, etc.)
- ✅ **Implementation details** hidden from Application

### Why `LlmProviders/Groq/`?
**Isolates provider-specific code:**
- Easy to add `LlmProviders/OpenAI/`
- Easy to swap providers
- Clear organization by external service

**Example structure for multiple providers:**
```
Infrastructure/
├── LlmProviders/
│   ├── Groq/
│   │   └── GroqLlmService.cs     ← implements ILlmService
│   ├── OpenAI/
│   │   └── OpenAILlmService.cs   ← also implements ILlmService
│   └── Anthropic/
│       └── ClaudeLlmService.cs   ← also implements ILlmService
└── Persistence/
    └── Repositories/
        └── CoverLetterRepository.cs
```

### Why separate `GroqModels.cs`?
**API-specific DTOs** - these models match Groq's API contract exactly.

**Separation of concerns:**
```
ILlmService.cs (Application) ← Generic, provider-agnostic
    ↓ maps to
GroqModels.cs (Infrastructure) ← Groq-specific request/response
```

---

## Layer 4: Api (Presentation/Entry Point) 🌐

### Location: `src/CoverLetter.Api/`

```
CoverLetter.Api/
├── Endpoints/
│   ├── CoverLetterEndpoints.cs   ← HTTP endpoints
│   └── HealthEndpoints.cs
├── Extensions/
│   └── ResultExtensions.cs       ← Result<T> → IResult (HTTP)
├── Middleware/
│   └── GlobalExceptionHandler.cs ← Catches exceptions → HTTP responses
├── Properties/
│   └── launchSettings.json       ← Dev settings (ports, URLs)
├── Program.cs                    ← Entry point, DI setup
├── appsettings.json              ← Configuration
├── appsettings.Development.json  ← Dev-specific config
├── CoverLetter.Api.http          ← HTTP test file
└── CoverLetter.Api.csproj
    Dependencies: Application, Infrastructure
```

### Purpose
**Entry point** - HTTP endpoints, dependency injection, configuration, middleware.

### Rules
- ✅ **Depends on Application** (sends commands via MediatR)
- ✅ **Depends on Infrastructure** (registers services)
- ✅ **Does NOT depend on Domain directly** (goes through Application)
- ✅ **HTTP-specific code** (controllers, endpoints, middleware)

### Why `Endpoints/` instead of `Controllers/`?
**Minimal APIs** - lighter than MVC controllers, better for small APIs.

```csharp
// Endpoints/CoverLetterEndpoints.cs
public static class CoverLetterEndpoints
{
    public static void MapCoverLetterEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/cover-letters/generate", async (request, mediator) => 
        {
            var command = new GenerateCoverLetterCommand(...);
            var result = await mediator.Send(command);
            return result.ToHttpResult();
        });
    }
}
```

**Benefits over Controllers:**
- ✅ Less boilerplate
- ✅ Better performance
- ✅ Easier to organize by feature

### Why `Extensions/`?
**Extension methods** - reusable utilities:
- `ResultExtensions.ToHttpResult()` - converts `Result<T>` to HTTP responses
- Could have: `ClaimsPrincipalExtensions.GetUserId()`, etc.

### Why `Middleware/`?
**HTTP pipeline components** that run for every request:
```
Request → Serilog → GlobalExceptionHandler → Endpoint → Response
```

**GlobalExceptionHandler** catches exceptions and converts them to proper HTTP responses (ProblemDetails).

### Why `Program.cs` at root?
**Entry point** - .NET 6+ top-level statements:
```csharp
var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure middleware
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

// Map endpoints
app.MapCoverLetterEndpoints();
app.MapHealthEndpoints();

app.Run();
```

---

## Tests Structure

```
tests/
└── CoverLetter.Application.Tests/
    └── UseCases/
        └── GenerateCoverLetter/
            ├── GenerateCoverLetterHandlerTests.cs
            └── GenerateCoverLetterValidatorTests.cs
```

**Mirror structure** - tests match source structure.

**Why only Application tests?**
- Domain: Pure logic, often tested via Application tests
- Infrastructure: Integration tests or manual testing (external APIs)
- Api: Integration tests or E2E tests

---

## Dependency Flow (Critical!)

```
Domain ← Application ← Infrastructure
  ↑          ↑              ↑
  └──────────┴──────────── Api
```

### Project References (.csproj)

**Domain:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- NO PROJECT REFERENCES! -->
</Project>
```

**Application:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\CoverLetter.Domain\CoverLetter.Domain.csproj" />
  </ItemGroup>
</Project>
```

**Infrastructure:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\CoverLetter.Application\CoverLetter.Application.csproj" />
    <ProjectReference Include="..\CoverLetter.Domain\CoverLetter.Domain.csproj" />
  </ItemGroup>
</Project>
```

**Api:**
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="..\CoverLetter.Application\CoverLetter.Application.csproj" />
    <ProjectReference Include="..\CoverLetter.Infrastructure\CoverLetter.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

**Rule:** Inner layers don't reference outer layers!

---

## Common Folder Names Explained

### `Common/`
Shared code within a layer:
- `Domain/Common/` - Result<T>, base entities
- `Application/Common/` - Behaviors, interfaces

### `Entities/`
Domain entities (DDD) - business objects with identity.

### `UseCases/`
Application use cases - organized by feature (Vertical Slice).

### `Behaviors/`
MediatR pipeline behaviors - cross-cutting concerns.

### `Interfaces/`
Abstractions for Dependency Inversion - Application defines, Infrastructure implements.

### `Extensions/`
Extension methods - static utility classes.

### `Middleware/`
ASP.NET Core middleware - HTTP pipeline components.

---

## Benefits of This Structure

| Benefit | How |
|---------|-----|
| **Testability** | Domain/Application have no external dependencies → easy to test |
| **Maintainability** | Features organized vertically → easy to find related code |
| **Flexibility** | Swap Infrastructure implementations without changing Application |
| **Independence** | Each layer can evolve independently |
| **Scalability** | Add new features by creating new use case folders |

---

## Anti-Patterns to Avoid

### ❌ **BAD: Application depends on Infrastructure**
```csharp
// Application/Handlers/MyHandler.cs
using CoverLetter.Infrastructure.Groq;  // ❌ WRONG!

public class MyHandler
{
    private readonly GroqLlmService _groq;  // ❌ Concrete type
}
```

### ✅ **GOOD: Application depends on abstraction**
```csharp
// Application/Handlers/MyHandler.cs
using CoverLetter.Application.Common.Interfaces;  // ✅ Correct

public class MyHandler
{
    private readonly ILlmService _llmService;  // ✅ Interface
}
```

---

## Quick Reference

| Where to put... | Layer | Folder |
|-----------------|-------|--------|
| Domain entities | Domain | `Entities/` |
| Result<T> pattern | Domain | `Common/` |
| MediatR commands | Application | `UseCases/{Feature}/` |
| MediatR handlers | Application | `UseCases/{Feature}/` |
| FluentValidation validators | Application | `UseCases/{Feature}/` |
| Service interfaces | Application | `Common/Interfaces/` |
| Pipeline behaviors | Application | `Common/Behaviors/` |
| Groq API implementation | Infrastructure | `LlmProviders/Groq/` |
| Database repositories | Infrastructure | `Persistence/Repositories/` |
| HTTP endpoints | Api | `Endpoints/` |
| Middleware | Api | `Middleware/` |
| Extension methods | Api | `Extensions/` |

---

## Summary

**Onion Architecture** keeps your code:
- ✅ **Testable** - core has no dependencies
- ✅ **Flexible** - swap implementations easily
- ✅ **Maintainable** - clear separation of concerns
- ✅ **Scalable** - add features without breaking existing code

**Key principle:** Dependency points inward, never outward!
