# Development Setup Guide

## Prerequisites
- .NET 10 SDK
- Docker & Docker Compose
- A Groq API key (get one free at https://console.groq.com/keys)

---

## Quick Setup (Recommended)

Run everything in Docker with a single command:

```bash
git clone https://github.com/Osama-Elzekred/ai-cover-letter-generator.git
cd ai-cover-letter-generator
docker-compose -f docker-compose.dev.yml up -d
```

Then:
1. **Get your Groq API key** from https://console.groq.com/keys
2. **Save it in the extension** → Settings tab → Groq API Key field

That's it! Everything runs:
- ✅ Backend API on port 5012
- ✅ PostgreSQL database
- ✅ Prometheus (metrics)
- ✅ Loki (logs)
- ✅ Grafana (dashboards)
- ✅ LaTeX compiler (PDF generation)

View the API docs: `http://localhost:5012/scalar/v1`

---

## Advanced Setup (Local .NET Development)

If you want to run the backend directly on your machine (not in Docker):

### Prerequisites
- .NET 10 SDK
- PostgreSQL running locally or in Docker

### Steps

```bash
# Clone and restore dependencies
git clone https://github.com/Osama-Elzekred/ai-cover-letter-generator.git
cd ai-cover-letter-generator
dotnet restore

# Set your Groq API key
cd src/CoverLetter.Api
dotnet user-secrets set "Groq:ApiKey" "gsk_your_key_here"

# Run migrations
dotnet ef database update

# Start the API
dotnet run
```

The API will be at: `http://localhost:5012/scalar/v1`

**Note:** You'll still need PostgreSQL running (use `docker-compose -f docker-compose.dev.yml up -d postgres` to start just the database)

---

## Configuration Files

| File | Purpose |
|------|---------|
| `docker-compose.dev.yml` | Complete local setup (Backend, Postgres, Loki, Prometheus, Grafana, LaTeX) |
| `appsettings.json` | API configuration (no secrets, safe to commit) |
| `.env.docker` | Environment variables for docker-compose (git-ignored) |

---

## Service URLs

When using Docker Compose:

| Service | URL | Purpose |
|---------|-----|---------|
| Backend API | http://localhost:5012/scalar/v1 | OpenAPI documentation |
| Health Check | http://localhost:5012/health | API health status |
| Prometheus | http://localhost:9090 | Metrics dashboard |
| Grafana | http://localhost:3000 | Log/metric visualization (admin/admin) |
| Database | localhost:5432 | PostgreSQL connection |

## Stopping Services

```bash
# Stop all services
docker-compose -f docker-compose.dev.yml down

# Stop and remove data
docker-compose -f docker-compose.dev.yml down -v
```

---

## Troubleshooting

### Docker Services Not Starting
```bash
# Check service status
docker-compose -f docker-compose.dev.yml ps

# View logs
docker-compose -f docker-compose.dev.yml logs coverletterapi
```

### Backend Returns 401 Unauthorized
- Save your Groq API key in the extension → Settings tab
- Or set `GROQ_API_KEY` environment variable before starting docker-compose

### Database Connection Errors
- Ensure postgres service is running: `docker ps | grep postgres`
- Check connection string uses `postgres` (not localhost) when running in Docker

### Can't Access API Docs
- Verify backend is running: `curl http://localhost:5012/health`
- Check if port 5012 is already in use: `netstat -an | findstr 5012` (Windows)

## Architecture & Learning

- **[ARCHITECTURE.md](docs/ARCHITECTURE.md)** - Project structure (Onion layers, MediatR, Result<T>)
- **[PROJECT-ROADMAP.md](docs/PROJECT-ROADMAP.md)** - Upcoming features and phases
- **[http-tests/](src/CoverLetter.Api/http-tests/)** - Example API requests
- **[Copilot Instructions](.github/copilot-instructions.md)** - Development guidelines
