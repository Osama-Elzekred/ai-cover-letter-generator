# **PROJECT-ROADMAP.md**  
(A full step-by-step plan from now until final system)

```md
# Project Roadmap – AI Cover Letter Generator

This roadmap outlines each phase of the system, from the first working API to a production-ready distributed architecture deployed on Kubernetes.

---

## Phase 1 — Build the Core API (Current Phase)
**Goal:** A working backend that generates cover letters.

### Tasks:
- Create .NET 10 minimal API project
- Add Groq LLaMA3 integration
- Implement `/generate-cover-letter`
- Add request/response models
- Test with real CV + job description
- Set up basic project structure

**Deliverable:** A functional API generating cover letters.

---

## Phase 2 — Build the Browser Extension
**Goal:** Extract job descriptions automatically.

### Tasks:
- Create Chrome Extension (Manifest v3)
- Popup UI (React or plain JS)
- Content script to detect LinkedIn job description
- Send requests to backend API
- User prompt storage (Chrome Storage API)
- Load CV file from user
- Display generated cover letter in popup

---

## Phase 3 — Add Database & Persistence
**Goal:** Save user CV, templates, and generated cover letters.

### Tasks:
- Add PostgreSQL
- Create basic schema:
  - Users
  - CVs
  - PromptTemplates
  - CoverLetterRequests
  - CoverLetters
- Add EF Core or Dapper
- Implement repositories
- Add `/history` endpoint

---

## Phase 4 — Architecting Microservices
**Goal:** Split services following clean boundaries.

### Services:
1. **API Gateway** (REST)
2. **Request Service** (gRPC)
3. **Generation Service** (REST)
4. **Worker Services** (Outbox processor)

---

## Phase 5 — Add Queue + Outbox Pattern
**Goal:** Event-driven architecture.

### Tasks:
- Add RabbitMQ or Kafka
- Add Outbox table in DB
- Add background worker in Request Service
- Add message consumer in Generation Service
- Push/consume generation requests via queue
- Ensure retry logic & at-least-once delivery

---

## Phase 6 — Observability (OpenTelemetry)
**Goal:** Metrics, logs, traces.

### Tasks:
- Integrate OTel SDK into all services
- Add metrics:
  - Queue processing latency
  - Request latency
  - LLM call duration
- Deploy:
  - Prometheus
  - Grafana
  - Loki (optional)

---

## Phase 7 — Containerization & Kubernetes
**Goal:** Deploy full system on k8s.

### Tasks:
- Write Dockerfiles for all services
- Write K8s manifests (Deployments, Services, Secrets)
- Add ingress + TLS
- Add OTel collector deployment
- Use Kustomize or Helm for environment configs

---

## Phase 8 — k6 Load Testing
**Goal:** Stress-test the system.

### Tasks:
- Write k6 scripts hitting:
  - API Gateway REST endpoints
  - gRPC endpoints
- Test concurrency (100–5000 users)
- Validate scaling & queue behavior
- Monitor on Grafana in real-time

---

## Phase 9 — CI/CD Pipeline
**Goal:** Automated builds and deployments.

### Tasks:
- GitHub Actions:
  - Build & test .NET apps
  - Build & push Docker images
  - Deploy to Kubernetes
- Add secret handling for cloud deployments
- Automated migrations (if using EF Core)

---

## Final Result
You will have built a **real-world distributed AI system** with:

- Microservices  
- Event-driven architecture  
- Kubernetes  
- Observability  
- Load testing  
- Modern .NET backend  
- Browser extension frontend  
