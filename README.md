# AI Cover Letter Generator

An intelligent system that generates personalized, professional cover letters based on:

- The user’s CV  
- Real job descriptions extracted from job sites (LinkedIn, Indeed, etc.)  
- A customizable prompt template per user  

The system consists of:

- A **browser extension** (Chrome/Edge) that detects job descriptions automatically.
- A **backend written in .NET 10**, using modern minimal APIs.
- An AI integration powered by **Groq LLaMA3-70B** (OpenAI-compatible API).
- A long-term architecture evolving into **distributed microservices**, including:
  - gRPC APIs
  - REST APIs
  - Message queue (RabbitMQ/Kafka)
  - Outbox pattern
  - Background workers
  - PostgreSQL Database
  - OpenTelemetry, Prometheus, Grafana
  - Kubernetes
  - CI/CD
  - k6 load testing  

This project is intended as a **full-stack learning system** covering advanced backend engineering patterns used in real companies.

---

## ⚙️ Technology Stack

### Frontend
- Chrome Extension (Manifest V3)
- React (optional)
- JavaScript/TypeScript
- Chrome Storage API
- Fetch API for backend communication

### Backend
- **.NET 10.0**
- Minimal APIs
- gRPC (future)
- Groq LLaMA3 via OpenAI-compatible API
- Outbox pattern
- Background workers
- PostgreSQL (future)
- Docker / Kubernetes (future)

### Infrastructure & DevOps
- Docker
- Kubernetes (k8s)
- Kustomize or Helm
- GitHub Actions CI/CD
- Prometheus + Grafana dashboards
- OpenTelemetry (metrics/traces/logs)
- RabbitMQ or Kafka (microservices communication)
- k6 for load testing

---

## 🎯 Current Phase (Phase 1)
- Single .NET 10 API  
- Endpoint: `POST /generate-cover-letter`
- Inputs: job description, CV text, optional prompt template  
- Output: generated cover letter  

---

## 🚀 Getting Started

### Quick Setup

```bash
git clone https://github.com/Osama-Elzekred/ai-cover-letter-generator.git
cd ai-cover-letter-generator
bash setup.sh
```

The setup script will guide you through configuration. Then:

```bash
cd src/CoverLetter.Api
dotnet run
```

Open: `http://localhost:5000/scalar/v1`

For detailed instructions, see [SETUP.md](SETUP.md).

### Prerequisites
- .NET 10 SDK
- Docker & Docker Compose
- A Groq API key (free at https://console.groq.com/keys)
