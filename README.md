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

## ✨ Highlights

- 🎨 **Modern WPF Client** — MVVM-first, responsive, themed desktop UI.
- 🔌 **REST API + SignalR** — Real-time events with a versioned ASP.NET Core API.
- 📊 **Data Integrations** — UEXCorp, Regolith.rocks, and GitHub data sync.
- 🧱 **Clean Architecture** — Domain, Application, Infrastructure, API, Presentation, Shared, Logging.
- 🗂 **Centralized Logging** — Serilog to console/file, optional Seq and OTEL exporters.
- 🔄 **Background Services** — Game log parsing and data sync workers.
- 🐳 **Dockerized API** — Container-ready for repeatable deployments.
- 🔐 **Secrets Hygiene** — Dev secrets with `dotnet user-secrets` or GitHub Encrypted Secrets.

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

## 🚀 Quick Start

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)  
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for API container)  
- Git + (optional) Rider or Visual Studio 2022+
