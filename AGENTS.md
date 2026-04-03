# Repository Guidelines

## Project Structure & Module Organization
- `PackTracker.Domain`, `.Application`, `.Infrastructure`, `.Shared`, and `.Logging` comprise the Clean Architecture core; keep business rules inside Domain and push EF Core, secrets, and integrations into Infrastructure.
- `PackTracker.Api` exposes REST + SignalR surfaces, while `PackTracker.Presentation` is the WPF MVVM client; docs and images sit under `docs/`.
- Tests reside in `tests/PackTracker.{Unit,Integration,Api}Tests`; mirror feature folders and place fixtures beside the code they validate.

## Build, Test, and Development Commands
- `dotnet restore PackTracker.sln && dotnet build PackTracker.sln` — reproduces the exact .NET 9.0.304 toolset defined in `global.json`.
- `dotnet run --project PackTracker.Api` — starts the API with Swagger + hot reload; ensure secrets/config are loaded first.
- `dotnet test PackTracker.sln --collect:"XPlat Code Coverage"` — runs every xUnit suite with coverlet output; resolve warnings before pushing.
- `dotnet publish PackTracker.Presentation -c Release` — generates a WPF artifact when UI changes need manual validation.

## Coding Style & Naming Conventions
- Repo-wide `Directory.Build.*` enforces `LangVersion=preview`, nullable reference types, file-scoped namespaces, and analyzers; fix warnings or document any suppression.
- Use 4-space indentation, PascalCase for types, camelCase for locals/fields, and suffix asynchronous members with `Async`.
- DTOs in `PackTracker.Shared` should stay versioned (`OrgSummaryV1`) so API and WPF consumers know when contracts evolve.

## Testing Guidelines
- xUnit + coverlet power all suites; favor the `ClassUnderTest_Scenario_Result` naming pattern and make Arrange/Act/Assert blocks clear.
- Unit tests isolate Domain/Application with fakes; EF Core, HTTP, or SignalR flows belong in Integration or API tests.
- Target ≥80% line coverage on touched files and add at least one automated test for every bug fix or feature toggle.

## Commit & Pull Request Guidelines
- Recent history is terse (`update`, `WIP`); migrate to `type(scope): imperative summary` (e.g., `feat(api): add contract ingestion`) going forward.
- Keep one logical change per commit/PR, reference issues (`Fixes #123`), and attach before/after screenshots when UI or Swagger contracts shift.
- PRs must outline testing (`dotnet test`, manual smoke) and configuration changes reviewers need to replay.

## Security & Configuration Tips
- Use `dotnet user-secrets` for sensitive values (e.g., `dotnet user-secrets set PackTracker:ApiKey <value>`); never commit secrets or `.env` files.
- For Docker parity, pass env vars via `docker run --env-file ops/dev.env packtracker-api`. Keep Serilog exporters (Seq/OTEL) opt-in through local overrides to avoid leaking telemetry.
