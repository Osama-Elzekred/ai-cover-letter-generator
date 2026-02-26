# Development Setup Guide

## Prerequisites
- .NET 10 SDK
- Docker & Docker Compose
- A Groq API key (get one free at https://console.groq.com/keys)

---

## Quick Setup (Recommended)

For new users, run the automated setup script:

```bash
git clone https://github.com/Osama-Elzekred/ai-cover-letter-generator.git
cd ai-cover-letter-generator
bash setup.sh
```

The script will:
- ✅ Restore .NET dependencies
- ✅ Prompt for your Groq API key and database password
- ✅ Configure user secrets
- ✅ Start PostgreSQL in Docker
- ✅ Build the LaTeX compiler image
- ✅ Run database migrations

After setup completes:
```bash
cd src/CoverLetter.Api
dotnet run
```

Then open: `http://localhost:5000/scalar/v1`

---

## Manual Setup (Step-by-Step)

If you prefer to understand each step or need to troubleshoot:

### 1. Clone & Install Dependencies

```bash
git clone https://github.com/Osama-Elzekred/ai-cover-letter-generator.git
cd ai-cover-letter-generator
dotnet restore
```

---

### 2. Set User Secrets for Development

User secrets are stored **outside the repo** and won't be committed.

```bash
cd src/CoverLetter.Api

# Set database connection string (use your own password)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=coverletter_dev;Username=postgres;Password=postgres"

# Set Groq API Key (get from https://console.groq.com/keys)
dotnet user-secrets set "Groq:ApiKey" "your-groq-api-key-here"
```

To verify secrets were stored:
```bash
dotnet user-secrets list
```

---

### 3. Start PostgreSQL

```bash
docker-compose -f docker-compose.dev.yml up -d
```

Verify it's running:
```bash
docker ps  # Should show coverletter_postgres
```

---

### 4. Build LaTeX Compiler Image

The API uses Docker to compile LaTeX → PDF in isolated containers.

```bash
# Build the LaTeX compiler image using docker-compose
docker-compose -f docker-compose.dev.yml build latex-compiler
```

**Why:** Sandboxes untrusted LaTeX code with no network, limited resources, and read-only filesystem (same pattern AWS Lambda uses). Each compilation spawns a fresh container and exits immediately.

---

### 5. Run Database Migrations

```bash
cd src/CoverLetter.Api
dotnet ef database update
```

---

### 6. Start the API

```bash
cd src/CoverLetter.Api
dotnet run
```

The API will start at `https://localhost:5001` or `http://localhost:5000`

**View API Docs:** Open your browser at `http://localhost:5000/scalar/v1`

---

### 7. Test the API

Use the HTTP test files:
```bash
# In VS Code with REST Client extension:
# Open src/CoverLetter.Api/http-tests/health.http
# Click "Send Request"
```

Or use curl:
```bash
curl http://localhost:5000/health
```

---

## Configuration Files

| File | Purpose | In Repo? | Notes |
|------|---------|----------|-------|
| `docker-compose.dev.yml` | Local Docker setup (Postgres + LaTeX) | ✅ YES | Default password: `postgres` (dev only) |
| `appsettings.json` | Shared API config with safe defaults | ✅ YES | No secrets, safe to commit |
| User Secrets | API keys & credentials | ❌ NO | Set via `dotnet user-secrets` or `setup.sh` |

**Note:** Sensitive credentials (Groq API key, custom DB passwords) are stored in user secrets outside the repo.

---

## Stopping Services

```bash
# Stop PostgreSQL
docker-compose -f docker-compose.dev.yml down

# Remove data (if needed)
docker-compose -f docker-compose.dev.yml down -v
```

---

## Troubleshooting

### Connection Refused
- Ensure Docker is running and PostgreSQL container started
- Check `docker logs coverletter_postgres`

### User Secrets Not Found
- Make sure you ran `dotnet user-secrets set` in `src/CoverLetter.Api` directory
- Check `dotnet user-secrets list` to verify they're stored

### Groq API Errors
- Verify your API key is set: `dotnet user-secrets list | grep Groq`
- Check your Groq account has remaining API quota

### LaTeX Compilation Fails
- Ensure Docker is running
- Verify the image exists: `docker images | grep latexmk-compiler`
- Rebuild if needed: `docker-compose -f docker-compose.dev.yml build latex-compiler`

---

## Next Steps

- Read [ARCHITECTURE.md](docs/ARCHITECTURE.md) to understand the project structure
- Check [http-tests](src/CoverLetter.Api/http-tests/) for example requests
- Review [PROJECT-ROADMAP.md](docs/PROJECT-ROADMAP.md) for upcoming phases
