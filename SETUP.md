# Development Setup Guide

## Prerequisites
- .NET 10 SDK
- Docker & Docker Compose
- PostgreSQL 18 (via Docker)

---

## 1. Clone & Install Dependencies

```bash
git clone https://github.com/Osama-Elzekred/ai-cover-letter-generator.git
cd ai-cover-letter-generator
dotnet restore
```

---

## 2. Configure Environment

### a) Copy Configuration Templates

```bash
# From the project root:
cp docker-compose.dev.yml.template docker-compose.dev.yml
cp src/CoverLetter.Api/appsettings.json.template src/CoverLetter.Api/appsettings.json
```

### b) Update `docker-compose.dev.yml`

Edit `docker-compose.dev.yml` and change the password:

```yaml
environment:
  POSTGRES_PASSWORD: your_secure_dev_password
```

### c) Set User Secrets for Development

User secrets are stored **outside the repo** and won't be committed.

```bash
cd src/CoverLetter.Api

# Set database connection string
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=coverletter_dev;Username=postgres;Password=your_secure_dev_password"

# Set Groq API Key
dotnet user-secrets set "Groq:ApiKey" "your-groq-api-key-here"
```

To verify secrets were stored:
```bash
dotnet user-secrets list
```

---

## 3. Start PostgreSQL

```bash
docker-compose -f docker-compose.dev.yml up -d
```

Verify it's running:
```bash
docker ps  # Should show coverletter_postgres
```

---

## 4. Run Database Migrations

```bash
cd src/CoverLetter.Api
dotnet ef database update
```

---

## 5. Start the API

```bash
cd src/CoverLetter.Api
dotnet run
```

The API will start at `https://localhost:5001` or `http://localhost:5000`

### View API Docs
Open your browser: `http://localhost:5000/scalar/v1`

---

## 6. Test the API

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
| `appsettings.json` | Shared config with safe defaults | ✅ YES | Copy from `.template` |
| `appsettings.Development.json` | Dev overrides (if needed) | ❌ NO | Use user secrets instead |
| `docker-compose.dev.yml` | Local Docker setup | ❌ NO | Copy from `.template` |
| User Secrets | Credentials (stored locally) | ❌ NO | Set via `dotnet user-secrets` |

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

---

## Next Steps

- Read [ARCHITECTURE.md](docs/ARCHITECTURE.md) to understand the project structure
- Check [http-tests](src/CoverLetter.Api/http-tests/) for example requests
- Review [PROJECT-ROADMAP.md](docs/PROJECT-ROADMAP.md) for upcoming phases
