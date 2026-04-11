# ─── Build Stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files (excludes Windows-only Presentation and test projects)
COPY PackTracker.Domain/PackTracker.Domain.csproj             PackTracker.Domain/
COPY PackTracker.Application/PackTracker.Application.csproj   PackTracker.Application/
COPY PackTracker.Infrastructure/PackTracker.Infrastructure.csproj PackTracker.Infrastructure/
COPY PackTracker.Api/PackTracker.Api.csproj                   PackTracker.Api/

RUN dotnet restore PackTracker.Api/PackTracker.Api.csproj

# Copy source
COPY PackTracker.Domain/       PackTracker.Domain/
COPY PackTracker.Application/  PackTracker.Application/
COPY PackTracker.Infrastructure/ PackTracker.Infrastructure/
COPY PackTracker.Api/          PackTracker.Api/

# Copy seed data (used by the crafting seed service at startup)
COPY PackTracker.Presentation/wwwroot/data/crafting-seed.json /seed/crafting-seed.json

RUN dotnet publish PackTracker.Api/PackTracker.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ─── Runtime Stage ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Place seed data at the fallback path the API resolves relative to ContentRoot (/app)
# Resolved path: Path.Combine(ContentRoot, "..", "PackTracker.Presentation", "wwwroot", "data", "crafting-seed.json")
RUN mkdir -p /PackTracker.Presentation/wwwroot/data
COPY --from=build /seed/crafting-seed.json /PackTracker.Presentation/wwwroot/data/crafting-seed.json

# Persistent log directory (mount a volume here)
RUN mkdir -p /app/Logs

EXPOSE 8080

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "PackTracker.Api.dll"]
