# AI Cover Letter Generator

AI Job Application Copilot that combines a Chrome extension with a backend API to help candidates apply faster with higher-quality, personalized content.

## What It Does

- Generates personalized cover letters using stored CV data plus the target job description.
- Creates customized CV outputs for specific jobs and returns both LaTeX source and compiled PDF.
- Auto-detects job context from LinkedIn job pages in the extension.
- Generates focused answers for LinkedIn application text fields using your CV and optional job context.
- Lets users save custom prompts per AI service (cover letter, CV customization, match analysis, textarea answers).
- Supports BYOK (Bring Your Own Key): users with their own API key bypass default rate limits.

## Core Product Capabilities

### 1) CV Ingestion and Reuse

- Upload CV as PDF, LaTeX, or plain text.
- Parse and store CV once, then reuse it across generation flows.
- Check CV existence and fetch stored CV by ID.

### 2) Cover Letter Generation

- Generate from stored CV ID + job description.
- Generate from direct CV text when no file upload is used.
- Support idempotency keys for safe retries on expensive POST requests.

### 3) CV Customization with LaTeX Pipeline

- Tailor CV for a target role using AI.
- Return both editable LaTeX and rendered PDF.
- Compile raw LaTeX to PDF through a dedicated API endpoint.

### 4) Job Match Analysis

- Analyze CV against a job description.
- Return match-oriented feedback for alignment and gaps.

### 5) LinkedIn Textarea Auto-Answer

- Inject AI assist buttons into LinkedIn application text inputs.
- Generate answers grounded in CV content and optional job context.
- Auto-fill generated answers back into the target field.

### 6) User Prompt Personalization

- Save, retrieve, and delete per-user prompts for:
  - cv-customization
  - cover-letter
  - match-analysis
  - textarea-answer
- Fall back to defaults when user prompts are not defined.

### 7) BYOK and Smart Rate Limiting

- Without user key: sliding-window limit on expensive AI endpoints.
- With user key: limiter bypass for higher throughput.
- Key lifecycle endpoints: save, check (masked), delete.

## API Feature Surface (Current)

- CV: parse file, parse text, customize, compile LaTeX, get by ID, exists check, match analysis.
- Cover letters: generate from CV ID, generate from direct text.
- Textarea answers: generate LinkedIn/application answers from CV.
- Settings: BYOK key management + per-service custom prompts.
- Prompts: inspect default template set.

## Architecture Notes

- Backend follows layered architecture (Domain, Application, Infrastructure, API).
- Request flow uses minimal APIs + MediatR pipeline + Result-based HTTP mapping.
- Error handling is centralized with ProblemDetails responses.
- Observability includes structured logs and metrics with Prometheus/Grafana/Loki integration.

## Observability & Monitoring

The project includes a complete observability stack:

- **Prometheus**: Collects metrics on HTTP requests, response times, endpoints.
- **Grafana**: Visual dashboards for monitoring performance and trends (http://localhost:3000).
- **Loki**: Aggregates structured logs from the API for querying and debugging.
- **Serilog**: Structured logging with configurable enrichment (timestamps, trace IDs, custom fields).

### Smart Error Messages

API errors are descriptive and actionable:
- **Authentication failures** (401/403): Clear messaging about invalid/expired API keys with guidance to update in Settings.
- **Rate limits** (429): Suggestions to use BYOK mode for higher throughput.
- **Validation errors** (400): Field-level error details for form corrections.
- **Upstream service errors**: Helpful hints with HTTP status codes.

## Getting Started

### Quick Setup

```bash
git clone https://github.com/Osama-Elzekred/ai-cover-letter-generator.git
cd ai-cover-letter-generator
bash setup.sh  # One-time setup: configure secrets, build services, run migrations
```

### Development Workflow (Recommended)

After setup, use this for fast, hot-reload development:

**Terminal 1 — Start services (PostgreSQL, Grafana, Prometheus, Loki):**
```bash

# Linux/macOS
bash dev-services.sh
```

**Terminal 2 — Run API with hot-reload:**
```bash
cd src/CoverLetter.Api
dotnet watch run
```

Then access:
- **API**: http://localhost:5012
- **API Docs**: http://localhost:5012/scalar/v1
- **Grafana**: http://localhost:3000 (admin/admin)

### Full Docker Stack (Alternative)

If you prefer everything in containers:

```bash
docker-compose -f docker-compose.dev.yml up
```

API will be available at http://localhost:5012.

### Prerequisites

- .NET 10 SDK
- Docker and Docker Compose
- Groq API key (get free key from https://console.groq.com/keys)

## Troubleshooting

### API Key Not Working
- **Local development**: Ensure your Groq API key was set during `bash setup.sh`. Check user secrets: `dotnet user-secrets list` in `src/CoverLetter.Api`.
- **Docker**: Verify `.env` file exists in project root with `GROQ_API_KEY=your-key`. If using Windows, restart Docker after updating `.env`.

### Loki Connection Error When Running Locally
- This is expected—when running API with `dotnet watch run`, logs try to send to `http://localhost:3100`.
- Either: (1) Start Loki with `docker-compose` first, or (2) Logs will fall back gracefully to console.

### PostgreSQL Connection Issues
- Ensure services are running: `docker-compose -f docker-compose.dev.yml up postgres`
- Check connection string in `appsettings.json` or user secrets matches your setup.
- Database is auto-created; migrations run on startup.

### LaTeX Compilation Errors
- LaTeX compiler requires Docker. Ensure it's running: `docker-compose -f docker-compose.dev.yml up` (includes all services).

For more setup details, see [SETUP.md](SETUP.md).
