<!-- PackTracker Social Preview -->
<p align="center">
  <img src="https://raw.githubusercontent.com/PackTracker.Presentation/Assets/Pack_Tracker.png" alt="PackTracker Banner"/>
</p>

# 🐺 PackTracker

[![.NET](https://img.shields.io/badge/.NET-9.0-blue?logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Desktop-5C2D91?logo=windows)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET-Core-512BD4?logo=dotnet)](https://learn.microsoft.com/aspnet/core)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker)](https://www.docker.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Build](https://img.shields.io/github/actions/workflow/status/YourOrg/PackTracker/build.yml?label=Build)](https://github.com/YourOrg/PackTracker/actions)

> PackTracker is a modern organization management system for **House Wolf** in the Star Citizen universe.  
> Built with **.NET 9, WPF, ASP.NET Core, EF Core, SignalR, and Serilog** using **Clean Architecture**.

---

## 📚 Table of Contents

- [Highlights](#-highlights)
- [Tech Stack](#-tech-stack)
- [Architecture](#-architecture)
- [Solution Layout](#-solution-layout)
- [Screenshots](#-screenshots)
- [Quick Start](#-quick-start)
- [Configuration](#-configuration)
- [Logging](#-logging)
- [API](#-api)
- [WPF Client](#-wpf-client)
- [Docker](#-docker)
- [Development](#-development)
- [Testing](#-testing)
- [CI/CD](#-cicd)
- [Roadmap](#-roadmap)
- [Contributing](#-contributing)
- [Security](#-security)
- [License](#-license)
- [Acknowledgements](#-acknowledgements)
- [Community](#-community)
- [Tags](#-tags)

---

## 🚧 Current Status

PackTracker is currently in an **active implementation/stabilization phase**.

What is clearly present in the repository today:
- multi-project .NET 9 solution structure
- WPF desktop shell and themed UI foundation
- ASP.NET Core API bootstrap
- Discord OAuth + JWT authentication flow scaffolding
- EF Core persistence layer with multiple operational domains
- request/dashboard/guide-related presentation surfaces
- settings persistence and first-run onboarding flow

What is **still being verified or completed**:
- end-to-end auth reliability from WPF → API → OAuth callback → token handoff
- feature completeness across request, guide, kill-tracking, and trading modules
- embedded API host wiring used by the desktop client
- test coverage for critical flows
- README claims vs currently shippable behavior

If you are adopting or contributing to PackTracker, assume the project is:
- **architecturally promising**
- **partially implemented**
- **not yet fully production-hardened**

Repository truth-pass tracking lives in:
- `IMPLEMENTATION_STATUS.md`

---

## ✨ Highlights

- 🎨 **Modern WPF Client** — MVVM-first, responsive, themed desktop UI with House Wolf visual cues.
- 🔌 **REST API + SignalR** — Real-time capable ASP.NET Core service foundation.
- 📊 **Data Integrations** — UEXCorp and Regolith integration paths are present and under active build-out.
- 🧱 **Clean Architecture Direction** — Domain, Application, Infrastructure, API, and Presentation projects are established.
- 🗂 **Centralized Logging** — Serilog-based logging is wired into the application bootstrap.
- 🔄 **Expandable Workflow Surface** — request handling, guide scheduling, trading, and telemetry domains are present in the repo.
- 🔐 **Secrets Hygiene** — local settings persistence exists with protected storage helpers for sensitive values.

---

## 🧰 Tech Stack

- **Languages:** C# (.NET 9)
- **Frontend:** WPF (MVVM, CommunityToolkit.Mvvm)
- **Backend:** ASP.NET Core (Controllers/Minimal APIs), SignalR
- **Data:** EF Core (PostgreSQL/SQLite), Caching (IMemoryCache / Redis-ready)
- **Logging:** Serilog (+ Exceptions, File/Console, Seq, OTEL)
- **Packaging:** Docker (API), Installer pipeline (WPF)
- **CI/CD:** GitHub Actions

---

## 🏗 Architecture

- **Domain** — Entities, value objects, domain events (no framework deps).
- **Application** — Use-cases (commands/queries), interfaces, validators.
- **Infrastructure** — EF Core DbContext, external services (UEX, Regolith), file/caching.
- **API** — HTTP controllers, SignalR hubs, hosted/background services.
- **Presentation** — WPF MVVM client consuming API/SignalR.
- **Shared** — Common DTOs/utilities used by API and Presentation.
- **Logging** — Central Serilog bootstrapping and enrichers.

---

## 🖼 Screenshots

> Place assets in `docs/images/` and reference them here.

- Dashboard — `docs/images/dashboard.png`  
- Settings — `docs/images/settings.png`  
- Swagger — `docs/images/swagger.png`  

---

## 🧭 Implementation Priorities

Current execution priority:
1. stabilize auth flow
2. verify embedded API startup path
3. confirm request/guide feature completeness
4. improve UI consistency and responsiveness
5. add tests around critical flows

Planned near-term vertical slices:
- Discord auth and profile bootstrap
- request workflow completion
- guide scheduling / assignment flow
- trading and telemetry refinement

## 🚀 Quick Start

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)  
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for API container)  
- Git + (optional) Rider or Visual Studio 2022+
