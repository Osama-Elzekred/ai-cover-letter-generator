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

## Getting Started

### Quick Setup

```bash
git clone https://github.com/Osama-Elzekred/ai-cover-letter-generator.git
cd ai-cover-letter-generator
bash setup.sh
```

Then run the API:

```bash
cd src/CoverLetter.Api
dotnet run
```

API docs are available at:

- http://localhost:5012/scalar/v1

For detailed setup, see [SETUP.md](SETUP.md).

### Prerequisites

- .NET 10 SDK
- Docker and Docker Compose
- Groq API key (for BYOK mode)
