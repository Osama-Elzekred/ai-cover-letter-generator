#!/bin/bash

# Quick start script for development services
# Starts: PostgreSQL, Loki, Prometheus, Grafana (but NOT the API)
# This allows you to run the API locally with `dotnet watch run` for hot-reload

echo "🚀 Starting development services..."
echo ""
echo "Services:"
echo "  • PostgreSQL: localhost:5432"
echo "  • Grafana: http://localhost:3000 (admin/admin)"
echo "  • Prometheus: http://localhost:9090"
echo "  • Loki: http://localhost:3100"
echo ""
echo "In another terminal, run:"
echo "  cd src/CoverLetter.Api && dotnet watch run"
echo ""
echo "API will be available at: http://localhost:5012"
echo "Docs: http://localhost:5012/scalar/v1"
echo ""
echo "Press Ctrl+C to stop services"
echo ""

docker-compose -f docker-compose.dev.yml up postgres loki prometheus grafana
