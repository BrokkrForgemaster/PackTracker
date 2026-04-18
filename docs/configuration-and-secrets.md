# Configuration and Secrets

## Sources

PackTracker reads configuration from:

1. `appsettings.json`
2. user secrets
3. environment variables
4. user-scoped persisted settings managed by `SettingsService`

## Required local secrets for embedded/local API hosting

- `ConnectionStrings:DefaultConnection`
- `Authentication:Jwt:Key`
- `Authentication:Discord:ClientId`
- `Authentication:Discord:ClientSecret`
- `Authentication:Discord:RequiredGuildId`

## Recommended setup

### API secrets

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your-connection-string>" --project PackTracker.Api
dotnet user-secrets set "Authentication:Jwt:Key" "<your-jwt-key>" --project PackTracker.Api
dotnet user-secrets set "Authentication:Discord:ClientId" "<discord-client-id>" --project PackTracker.Api
dotnet user-secrets set "Authentication:Discord:ClientSecret" "<discord-client-secret>" --project PackTracker.Api
dotnet user-secrets set "Authentication:Discord:RequiredGuildId" "<guild-id>" --project PackTracker.Api
```

### Desktop secrets

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your-connection-string>" --project PackTracker.Presentation
dotnet user-secrets set "Authentication:Jwt:Key" "<your-jwt-key>" --project PackTracker.Presentation
dotnet user-secrets set "Authentication:Discord:ClientId" "<discord-client-id>" --project PackTracker.Presentation
dotnet user-secrets set "Authentication:Discord:ClientSecret" "<discord-client-secret>" --project PackTracker.Presentation
dotnet user-secrets set "Authentication:Discord:RequiredGuildId" "<guild-id>" --project PackTracker.Presentation
```

## Remediation completed in this refactor

- removed the committed desktop database connection string from `PackTracker.Presentation/appsettings.json`
- removed the API host's hard-coded JWT fallback key
- retained encrypted user-scoped storage for sensitive persisted values

## Guidance

- Keep `appsettings.json` non-secret.
- Prefer user secrets for development.
- Prefer environment variables or deployment secret stores in CI/CD and production.
