# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

WORKDIR /build

# Copy solution and project files
COPY ["src/CoverLetter.Api/CoverLetter.Api.csproj", "src/CoverLetter.Api/"]
COPY ["src/CoverLetter.Application/CoverLetter.Application.csproj", "src/CoverLetter.Application/"]
COPY ["src/CoverLetter.Infrastructure/CoverLetter.Infrastructure.csproj", "src/CoverLetter.Infrastructure/"]
COPY ["src/CoverLetter.Domain/CoverLetter.Domain.csproj", "src/CoverLetter.Domain/"]

# Restore dependencies
RUN dotnet restore "src/CoverLetter.Api/CoverLetter.Api.csproj"

# Copy source code
COPY . .

# Publish the application
RUN dotnet publish "src/CoverLetter.Api/CoverLetter.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

# Copy published application from builder stage
COPY --from=builder /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:5012

# Expose port
EXPOSE 5012

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD dotnet --version || exit 1

# Start the application
ENTRYPOINT ["dotnet", "CoverLetter.Api.dll"]
