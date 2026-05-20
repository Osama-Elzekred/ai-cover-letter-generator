#!/bin/bash
set -e

echo "======================================"
echo "  AI Cover Letter Generator Setup"
echo "======================================"
echo ""

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Check prerequisites
echo "Checking prerequisites..."

if ! command -v docker &> /dev/null; then
    echo -e "${RED}❌ Docker not found. Please install Docker Desktop first.${NC}"
    exit 1
fi

if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}❌ .NET SDK not found. Please install .NET 10 SDK first.${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Prerequisites check passed${NC}"
echo ""

# Restore .NET dependencies
echo "📦 Restoring .NET dependencies..."
dotnet restore
echo ""

# Configure user secrets
echo "🔐 Configuring user secrets..."
echo ""

cd src/CoverLetter.Api

# Prompt for database password
read -p "Enter PostgreSQL password for development (default: postgres): " DB_PASSWORD
DB_PASSWORD=${DB_PASSWORD:-postgres}

# Prompt for Groq API key
echo ""
echo "Get your free Groq API key from: https://console.groq.com/keys"
read -p "Enter your Groq API key: " GROQ_API_KEY

if [ -z "$GROQ_API_KEY" ]; then
    echo -e "${RED}❌ Groq API key is required${NC}"
    exit 1
fi

# Set user secrets
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=coverletter_dev;Username=postgres;Password=$DB_PASSWORD"
dotnet user-secrets set "Groq:ApiKey" "$GROQ_API_KEY"

echo -e "${GREEN}✅ User secrets configured${NC}"
echo ""

# Go back to root
cd ../..

# Create .env file for docker-compose
echo "Creating .env file for Docker..."
cat > .env << EOF
GROQ_API_KEY=$GROQ_API_KEY
EOF
echo -e "${GREEN}✅ .env file created${NC}"
echo ""

# Start PostgreSQL
echo "🐘 Starting PostgreSQL..."
docker-compose -f docker-compose.dev.yml up -d
echo ""

# Wait for PostgreSQL to be ready
echo "⏳ Waiting for PostgreSQL to be ready..."
sleep 5

# Build LaTeX compiler image
echo "📄 Building LaTeX compiler image..."
docker-compose -f docker-compose.dev.yml build latex-compiler
echo ""

# Run database migrations
echo "🗄️  Running database migrations..."
cd src/CoverLetter.Api
dotnet ef database update
cd ../..
echo ""

echo -e "${GREEN}======================================"
echo "  ✅ Setup Complete!"
echo "======================================${NC}"
echo ""
echo "📝 Development Workflow:"
echo ""
echo "Option 1: Local Development (Recommended) - Hot Reload"
echo "  Terminal 1: docker-compose -f docker-compose.dev.yml up postgres loki prometheus grafana"
echo "  Terminal 2: cd src/CoverLetter.Api && dotnet watch run"
echo ""
echo "Option 2: Full Docker Stack"
echo "  docker-compose -f docker-compose.dev.yml up"
echo ""
echo "🔍 Access Points:"
echo "  • API: http://localhost:5012"
echo "  • Docs: http://localhost:5012/scalar/v1"
echo "  • Grafana: http://localhost:3000 (admin/admin)"
echo ""
