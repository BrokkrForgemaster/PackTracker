# PackTracker

<p align="center">
  <img src="PackTracker.Presentation/Assets/HousewolfBanner.png" alt="PackTracker banner" width="100%" />
</p>

<p align="center">
  <strong>Operations, logistics, crafting, and member coordination for Star Citizen organizations.</strong>
</p>

<p align="center">
  <img alt=".NET 9" src="https://img.shields.io/badge/.NET-9-512BD4?style=for-the-badge&logo=dotnet" />
  <img alt="WPF Desktop" src="https://img.shields.io/badge/Desktop-WPF-0C7BDC?style=for-the-badge" />
  <img alt="ASP.NET Core API" src="https://img.shields.io/badge/API-ASP.NET_Core-5C2D91?style=for-the-badge" />
  <img alt="SignalR" src="https://img.shields.io/badge/Realtime-SignalR-059669?style=for-the-badge" />
  <img alt="PostgreSQL" src="https://img.shields.io/badge/Database-PostgreSQL-336791?style=for-the-badge&logo=postgresql" />
  <img alt="Clean Architecture" src="https://img.shields.io/badge/Architecture-Clean-111827?style=for-the-badge" />
</p>

---

## Overview

PackTracker is a desktop-first command platform built for Star Citizen groups that need more than a spreadsheet and a Discord server. It combines a WPF client, a shared ASP.NET Core API, real-time updates through SignalR, and a PostgreSQL-backed data layer to coordinate requests, logistics, crafting, trading data, and member activity in one place.

The repository is organized around Clean Architecture boundaries so the same core application logic can power both the standalone API and the embedded API hosted by the desktop shell.

---

## Table of Contents

- [Highlights](#highlights)
- [Feature Areas](#feature-areas)
- [Architecture](#architecture)
- [Repository Layout](#repository-layout)
- [Quick Start](#quick-start)
- [Configuration and Secrets](#configuration-and-secrets)
- [Testing](#testing)
- [Deployment and Publishing](#deployment-and-publishing)
- [Documentation](#documentation)
- [Screenshot Notes](#screenshot-notes)

---

## Highlights

| Area | What it covers |
| --- | --- |
| Operations Dashboard | Central landing area for coordination, visibility, and real-time activity |
| Request Workflows | Crafting, procurement, and assistance requests with status tracking |
| Trading Hub | UexCorp-backed commodity intelligence for route and profit decisions |
| Blueprint Explorer | Blueprint lookup, ownership tracking, and crafting input visibility |
| Discord Integration | Authentication and org-connected workflows |
| Shared Hosting Model | Same API composition for standalone server and embedded desktop host |

<details>
<summary><strong>Why this structure matters</strong></summary>

PackTracker is not just a UI shell. The desktop app hosts or connects to an API, uses shared application services, and keeps infrastructure concerns out of the domain layer. That reduces drift between local desktop usage and server-backed deployment, which is especially important for authentication, middleware, database initialization, and real-time messaging.

</details>

---

## Feature Areas

### Command Surface

<p align="center">
  <img src="PackTracker.Presentation/Assets/Pack_Tracker.png" alt="PackTracker title art" width="720" />
</p>

PackTracker currently exposes a broad operations surface across the desktop app and API:

- dashboard and organization chat
- trading hub with commodity route analysis
- blueprint exploration and ownership tracking
- crafting request management
- procurement request workflows
- assistance and general support requests
- profiles, authentication, and Discord-connected access
- wiki sync and external data ingestion

<details>
<summary><strong>Implemented views and controllers</strong></summary>

Desktop views include `DashboardView`, `UexView`, `BlueprintExplorerView`, `CraftingRequestsView`, `ProcurementRequestsView`, `RequestsView`, `ProfileView`, `SettingsView`, and `HelpView`.

API controllers include `DashboardController`, `UexController`, `BlueprintsController`, `BlueprintOwnershipController`, `CraftingRequestsController`, `AssistanceRequestsController`, `ProfilesController`, `DiscordController`, `AuthController`, `RequestController`, `GuideRequestController`, and `WikiSyncController`.

</details>

### Operations and Real-Time Coordination (Coming Soon)

[//]: # (<p align="center">)

[//]: # (  <img src="PackTracker.Presentation/Assets/tacops.png" alt="Operations art" width="720" />)

[//]: # (</p>)

The dashboard is designed to act as a live coordination surface instead of a static admin panel. SignalR is used for real-time communication and request broadcasting so operators can react without manual refresh loops.

<details>
<summary><strong>More detail</strong></summary>

- channel-based communication and direct messaging are documented in the desktop user guide
- online presence and fleet coordination are part of the dashboard experience
- the API exposes a SignalR hub and shared composition path for both standalone and embedded hosting
- the desktop shell can either connect to a remote API or host the local API automatically when configured for loopback use

</details>

### Trading, Blueprints, and Crafting

<p align="center">
<img src="docs/images/trading_hub.png" alt="Crafting and blueprint systems" width="720" />
<img src="docs/images/blueprint.png" alt="Crafting and blueprint systems" width="720" />
<img src="docs/images/crafting_request.png" alt="Crafting and blueprint systems" width="720" />
<img src="docs/images/procurement.png" alt="Crafting and blueprint systems" width="720" />
</p>

PackTracker goes beyond task tracking by integrating market intelligence and blueprint data into request workflows. That gives users a path from planning, to sourcing, to fulfillment.

<details>
<summary><strong>More detail</strong></summary>

- the Trading Hub pulls commodity data from UexCorp
- route analysis includes price, ROI, and profit-per-SCU style decision support
- the Blueprint Explorer provides searchable crafting data and ownership tracking
- crafting requests can be initiated from blueprint flows
- procurement workflows support material acquisition for crafting and operations
- wiki sync services ingest blueprint and item data from external sources

</details>

### Requests, Logistics, and Member Support

<p align="center">
  <img src="docs/images/request.png" alt="Logistics and request workflows" width="720" />
</p>

The request system is split by operational intent so teams can manage general assistance separately from production and logistics work.

<details>
<summary><strong>More detail</strong></summary>

- assistance requests cover combat, mining, medical, and general operational support
- crafting requests track status, assignees, and completion
- procurement requests support material logistics and linked fulfillment
- dashboard summaries aggregate active work into a command-friendly view
- tests in `PackTracker.ApiTests` cover request behavior and API interaction patterns

</details>

### Identity, Roles, and Recognition

<p align="center">
  <img src="docs/images/procurement.png" alt="Leadership and roles" width="720" />
<img src="docs/images/medals.png" alt="Crafting and blueprint systems" width="720" />
<img src="docs/images/recruitment.png" alt="Crafting and blueprint systems" width="720" />
</p>

PackTracker includes organization-facing identity and recognition systems, including role-aware behavior and medal assets used by the desktop application.

<details>
<summary><strong>More detail</strong></summary>

- Discord OAuth is part of the authentication flow
- guild membership requirements can be configured through settings
- user profiles track organization identity information
- medal images and recognition assets live under `PackTracker.Presentation/Assets/medals`
- role and profile data are used across request ownership, dashboard summaries, and authorization-sensitive flows

</details>

---

## Architecture

PackTracker follows Clean Architecture boundaries with shared composition between the desktop-hosted API and the standalone API.

```mermaid
flowchart LR
    WPF[PackTracker.Presentation<br/>WPF Desktop Client]
    API[PackTracker.Api<br/>REST + SignalR]
    APP[PackTracker.Application<br/>Use Cases + Contracts]
    DOMAIN[PackTracker.Domain<br/>Business Model]
    INFRA[PackTracker.Infrastructure<br/>EF Core + Integrations]
    LOG[PackTracker.Logging<br/>Shared Serilog Setup]
    DB[(PostgreSQL)]
    EXT[Discord / UEX / Wiki APIs]

    WPF --> API
    WPF --> APP
    API --> APP
    APP --> DOMAIN
    API --> INFRA
    WPF --> INFRA
    INFRA --> DB
    INFRA --> EXT
    API --> LOG
    WPF --> LOG
```

<details>
<summary><strong>Layer responsibilities</strong></summary>

| Layer | Responsibility |
| --- | --- |
| `PackTracker.Domain` | Entities, enums, and business state with no infrastructure dependency |
| `PackTracker.Application` | DTOs, service contracts, options, and orchestration logic |
| `PackTracker.Infrastructure` | EF Core, settings persistence, token services, updater logic, and external integrations |
| `PackTracker.Api` | Controllers, middleware, hosting composition, and SignalR |
| `PackTracker.Presentation` | WPF views, view models, embedded API lifecycle, and desktop startup |
| `PackTracker.Logging` | Shared logging configuration and Serilog wiring |

</details>

<details>
<summary><strong>Hosting model</strong></summary>

The desktop app can run against a remote API or spin up the embedded API locally. Both the embedded and standalone paths share the same registration and middleware composition through `PackTracker.Api/Hosting/PackTrackerApiComposition.cs`, which reduces behavioral drift between environments.

</details>

---

## Repository Layout

```text
PackTracker/
|- PackTracker.Api/
|- PackTracker.Application/
|- PackTracker.Domain/
|- PackTracker.Infrastructure/
|- PackTracker.Logging/
|- PackTracker.Presentation/
|- tests/
|  |- PackTracker.UnitTests/
|  |- PackTracker.IntegrationTests/
|  `- PackTracker.ApiTests/
`- docs/
```

<details>
<summary><strong>Additional notes</strong></summary>

- `docs/` contains architecture, deployment, configuration, updater, and refactor notes
- `tools/` contains small utilities such as `AdminProbe`
- `installer/` contains the installer definition used for packaging
- `publish/` and `artifacts/` may contain local build outputs

</details>

---

## Quick Start

### Prerequisites

- .NET SDK `9.0.304`
- Windows for running the WPF desktop application
- PostgreSQL for local embedded or standalone API scenarios

### Restore and Build

```powershell
dotnet restore PackTracker.sln
dotnet build PackTracker.sln
```

### Run the Standalone API

```powershell
dotnet run --project PackTracker.Api
```

### Run the Desktop App

```powershell
dotnet run --project PackTracker.Presentation
```

<details>
<summary><strong>Embedded API behavior</strong></summary>

- if `AppSettings.ApiBaseUrl` points at a remote absolute URL, the desktop client uses that API
- if `ApiBaseUrl` is empty or loopback, the desktop client starts the embedded API locally
- the effective local loopback URL is then written back into user settings for use by the client

</details>

---

## Configuration and Secrets

PackTracker reads configuration from:

1. `appsettings.json`
2. user secrets
3. environment variables
4. user-scoped persisted settings managed by the settings service

### Recommended local secret setup

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your-connection-string>" --project PackTracker.Api
dotnet user-secrets set "Authentication:Jwt:Key" "<your-jwt-key>" --project PackTracker.Api
dotnet user-secrets set "Authentication:Discord:ClientId" "<discord-client-id>" --project PackTracker.Api
dotnet user-secrets set "Authentication:Discord:ClientSecret" "<discord-client-secret>" --project PackTracker.Api
dotnet user-secrets set "Authentication:Discord:RequiredGuildId" "<guild-id>" --project PackTracker.Api
```

<details>
<summary><strong>Desktop secrets</strong></summary>

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your-connection-string>" --project PackTracker.Presentation
dotnet user-secrets set "Authentication:Jwt:Key" "<your-jwt-key>" --project PackTracker.Presentation
dotnet user-secrets set "Authentication:Discord:ClientId" "<discord-client-id>" --project PackTracker.Presentation
dotnet user-secrets set "Authentication:Discord:ClientSecret" "<discord-client-secret>" --project PackTracker.Presentation
dotnet user-secrets set "Authentication:Discord:RequiredGuildId" "<guild-id>" --project PackTracker.Presentation
```

</details>

<details>
<summary><strong>Required local values for embedded or local API hosting</strong></summary>

- `ConnectionStrings:DefaultConnection`
- `Authentication:Jwt:Key`
- `Authentication:Discord:ClientId`
- `Authentication:Discord:ClientSecret`
- `Authentication:Discord:RequiredGuildId`

</details>

---

## Testing

Run the full solution test suite with coverage:

```powershell
dotnet test PackTracker.sln --collect:"XPlat Code Coverage"
```

You can also run the suites individually:

```powershell
dotnet test tests\PackTracker.UnitTests\PackTracker.UnitTests.csproj
dotnet test tests\PackTracker.ApiTests\PackTracker.ApiTests.csproj
dotnet test tests\PackTracker.IntegrationTests\PackTracker.IntegrationTests.csproj
```

<details>
<summary><strong>Test intent by suite</strong></summary>

- `UnitTests` isolate domain and presentation-facing logic
- `ApiTests` validate controller and request behavior
- `IntegrationTests` cover embedded host validation and cross-layer flows

</details>

---

## Deployment and Publishing

### Desktop Publish

```powershell
dotnet publish PackTracker.Presentation -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true
```

Published output:

```text
PackTracker.Presentation\bin\Release\net9.0-windows\win-x64\publish\
```

### Standalone API

```powershell
dotnet run --project PackTracker.Api
```

<details>
<summary><strong>Deployment notes</strong></summary>

- `appsettings.json` is kept outside the single-file bundle so local configuration remains editable
- container or cloud deployments should use environment variables or mounted config instead of committed secrets
- the updater remains user-triggered from the desktop shell
- Docker-based local parity is available through `docker-compose.yml`

</details>

---

## Documentation

- [Architecture](docs/architecture.md)
- [Local Development](docs/local-development.md)
- [Configuration and Secrets](docs/configuration-and-secrets.md)
- [Deployment](docs/deployment.md)
- [Updater Flow](docs/updater-flow.md)
- [Dependency Rules](docs/dependency-rules.md)
- [Refactor Report](docs/refactor-report.md)

---

## Screenshot Notes

This README already uses real repository assets so it renders cleanly today, but it is also structured to support richer product screenshots later.

<details>
<summary><strong>Recommended screenshot convention</strong></summary>

If you want section-specific UI screenshots, add them under a dedicated folder such as `docs/images/` and swap the current image tags section by section. A clean convention would be:

```text
docs/images/
|- dashboard.png
|- trading-hub.png
|- blueprint-explorer.png
|- crafting-center.png
|- procurement-center.png
`- request-hub.png
```

That will keep README asset links short and easy to maintain.

</details>

---

## License

This repository is licensed under the [MIT License](LICENSE).
