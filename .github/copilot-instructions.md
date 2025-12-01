# GitHub Copilot – Project Context & Guidelines

## Project Overview
This repository contains an **AI-powered cover letter generation system** that consists of:

- A **browser extension** (Chrome/Edge) that extracts job descriptions from job pages (e.g., LinkedIn) and allows the user to generate a personalized cover letter.
- A **.NET 10 backend** that exposes REST and gRPC endpoints, communicates with an LLM provider (Groq), stores requests, processes messages, and returns generated cover letters.
- A future architecture based on **microservices**, **message queues**, **observability**, and **Kubernetes** deployment.

The ultimate goal is to simulate a **real-world distributed system** like those used in large companies, covering:
- gRPC, REST APIs
- Message queues (RabbitMQ/Kafka)
- Outbox pattern
- Background workers
- OpenTelemetry, Prometheus, Grafana
- Docker & Kubernetes
- CI/CD automation
- Load testing using k6

Copilot should generate code that supports this long-term architecture.

---

## Current Phase (Phase 1)
The backend is currently a **single .NET 10 minimal API** that:

- Exposes `POST /generate-cover-letter`
- Accepts:
  - Job description
  - CV text
  - Optional custom prompt template
- Uses the **Groq API (Llama3-70B)** through OpenAI-compatible `chat/completions`
- Returns the generated cover letter

Copilot should write all code assuming:
- .NET **10.0**
- C# **14**
- Minimal APIs (not controllers)
- Dependency Injection is used
- HttpClient is injected via `AddHttpClient`
- Asynchronous programming everywhere (`async/await`)
- Record types for request/response models

---

## Upcoming Architecture (Phases 2–7)
Copilot should also be aware of the upcoming system design and generate code that fits into this future structure.

### Planned Services:
1. **API Gateway**
   - ASP.NET Core REST
   - Routes requests to internal microservices
   - Auth, rate limiting, validation layer

2. **Service A – Request Service (gRPC)**
   - Receives requests (job description + CV)
   - Stores requests into DB
   - Writes events into **Outbox table**
   - Background worker sends events into the message queue

3. **Message Queue**
   - RabbitMQ or Kafka
   - Used for async communication between services

4. **Service B – Generation Service (REST)**
   - Subscribes to queue messages
   - Loads CV + template
   - Calls LLM provider (Groq)
   - Stores generated cover letters in DB
   - Exposes REST GET endpoints

5. **Database (PostgreSQL)**
   - Tables:
     - Users
     - CVs
     - PromptTemplates
     - CoverLetterRequests
     - OutboxMessages
     - CoverLetters

6. **Observability**
   - OpenTelemetry (metrics + traces)
   - Prometheus (scraping)
   - Grafana (dashboards)
   - Loki (logs)

7. **Deployment**
   - Docker (containers)
   - Kubernetes
   - Kustomize or Helm
   - GitHub Actions for CI/CD

Copilot should generate code that is easily migratable toward this architecture.

---

## Coding Guidelines For Copilot

### Language & Framework
- Use **C# 14** features where beneficial.
- Target **.NET 10.0**.
- Use **minimal APIs**.
- Prefer modern patterns like:
  - Top-level statements
  - `app.MapPost()` instead of controllers
  - Dependency injection with `builder.Services.Add…`
  - Record types (`record`) for DTOs

### API Style
- Return `IResult` or `Results.Ok()` from endpoints.
- Use correct HTTP status codes.
- Always validate input before processing.

### LLM Integration
- Use a typed Groq client via dependency injection.
- All LLM calls should be asynchronous.
- Serialize JSON using `System.Text.Json`.
- Follow OpenAI-compatible `chat/completions` schema.

### Error Handling
- Return meaningful, structured errors.
- Wrap external API failures with detailed messages (`ProblemDetails`).
- Avoid swallowing exceptions.

### Code Organization
Copilot should:
- Keep the project modular.
- Place services in a `Services/` folder.
- Place models in a `Models/` folder.
- Place utilities/helpers in a `Helpers/` folder.
- Avoid bloating `Program.cs`.

### Naming Conventions
- Services: `CoverLetterService`, `GroqChatClient`, `OutboxProcessorWorker`
- DTOs: `GenerateCoverLetterRequest`, `GenerateCoverLetterResponse`
- Repositories: `ICoverLetterRepository`, `IOutboxRepository`

### Async Always
- All methods should use `async Task<T>`.
- No `.Result` or `.Wait()` calls.
- Use `CancellationToken` in long-running operations.

---

## When Copilot Writes Future Code
When generating code for upcoming phases, Copilot should:

- Respect clean architecture (separate domains, infrastructure, application layers).
- Use interfaces and DI for testability.
- Use repository pattern for DB interactions.
- Prepare for message queue communication.
- Keep services independent and stateless.
- Stick to modern .NET 10 practices.

---

## When Copilot Writes Browser Extension Code
Copilot should generate:
- A Chrome Manifest v3 extension.
- A service worker (`background.js` or `service-worker.js`).
- A content script that extracts job info from LinkedIn.
- A popup UI built in:
  - HTML/CSS/JS, or
  - React/Next.js (if chosen)

The extension should:
- Call the backend API Gateway
- Use `fetch` with proper CORS settings
- Store user CV and prompt in Chrome Storage

---

## DO NOT DO:
Copilot should avoid:
- Generating code for .NET 6 or .NET 8
- Using MVC controllers (unless explicitly requested)
- Using obsolete libraries (Newtonsoft.Json instead of System.Text.Json, unless needed)
- Hardcoding secrets
- Blocking calls (`.Wait()` or `.Result`)
- Mixing concerns (DB logic inside endpoints)
- Writing code that breaks minimal API conventions

---

## Summary
Copilot should treat this repository as a **modern, evolving distributed system** built on:

- .NET **10**
- C# **14**
- Minimal APIs
- Groq LLaMA3
- Event-driven microservices
- Kubernetes-native architecture
- Clean, modular, asynchronous, testable code

Every suggestion should move the project closer toward the long-term goal.

