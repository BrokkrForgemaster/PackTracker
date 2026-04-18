# PackTracker

PackTracker is a .NET 9 solution for a WPF desktop client with a local/embedded ASP.NET Core API. The codebase is organized around Clean Architecture boundaries so desktop workflows, API hosting, persistence, logging, and update delivery can evolve independently without changing user-facing behavior.

## Solution layout

- `PackTracker.Domain`: entities, enums, domain rules
- `PackTracker.Application`: contracts, DTOs, options, use-case-facing abstractions
- `PackTracker.Infrastructure`: EF Core, external services, persistence, security, updater implementation
- `PackTracker.Api`: API host, controllers, middleware, SignalR hub, shared API composition
- `PackTracker.Presentation`: WPF desktop shell, views, view models, embedded API startup
- `PackTracker.Logging`: centralized structured logging configuration and logging abstraction wiring
- `tests/*`: unit, API, and integration coverage

## What changed in this refactor

- Extracted structured logging into `PackTracker.Logging`
- Consolidated standalone API and embedded API onto a shared composition path
- Removed hard-coded JWT fallback behavior from API hosting
- Reworked updater configuration to use typed `UpdateOptions`
- Removed committed database credentials from desktop configuration
- Added updater tests and updated integration host tests to match the shared bootstrap path

## Build, test, publish

```powershell
dotnet restore PackTracker.sln
dotnet build PackTracker.sln
dotnet test tests\PackTracker.UnitTests\PackTracker.UnitTests.csproj
dotnet test tests\PackTracker.ApiTests\PackTracker.ApiTests.csproj
dotnet test tests\PackTracker.IntegrationTests\PackTracker.IntegrationTests.csproj
dotnet publish PackTracker.Presentation -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true
```

## Configuration

Do not store secrets in `appsettings.json`.

Use one of:

- `dotnet user-secrets`
- environment variables
- the user-scoped settings file managed by `SettingsService`

Start with the guides in:

- [docs/local-development.md](docs/local-development.md)
- [docs/configuration-and-secrets.md](docs/configuration-and-secrets.md)
- [docs/deployment.md](docs/deployment.md)

## Documentation

- [Architecture](docs/architecture.md)
- [Dependency Rules](docs/dependency-rules.md)
- [Local Development](docs/local-development.md)
- [Deployment](docs/deployment.md)
- [Updater Flow](docs/updater-flow.md)
- [Configuration and Secrets](docs/configuration-and-secrets.md)
- [Refactor Report](docs/refactor-report.md)
