# Architecture Overview

## Current state

PackTracker is a desktop-first application with:

- a WPF presentation layer
- a local or standalone ASP.NET Core API
- EF Core persistence
- external integrations for Discord, UEX, and wiki sync
- structured logging shared across desktop, API, and updater flows

## Layer responsibilities

### Domain

- entities and enums
- business state
- no infrastructure dependencies

### Application

- DTOs
- options
- service interfaces
- orchestration contracts consumed by API and presentation

### Infrastructure

- EF Core `AppDbContext`
- settings persistence and secret protection
- JWT/token services
- updater implementation
- external HTTP integrations

### API

- controllers
- middleware
- SignalR hub
- shared API composition in `Hosting/PackTrackerApiComposition.cs`

### Presentation

- WPF views and view models
- desktop startup
- embedded API lifecycle management via `ApiHostedService`

### Logging

- centralized Serilog configuration
- logging abstraction registration
- shared file sink and enrichment rules

## Hosting model

The standalone API and the embedded desktop API now share the same service registration, middleware pipeline, and database initialization path through `PackTrackerApiComposition`.

This eliminates the prior drift where:

- embedded hosting had a separate pipeline
- middleware differed by host
- startup behavior duplicated API configuration logic

## Key composition points

- `PackTracker.Api/Program.cs`
- `PackTracker.Api/Hosting/PackTrackerApiComposition.cs`
- `PackTracker.Presentation/App.xaml.cs`
- `PackTracker.Presentation/ApihostedService.cs`
- `PackTracker.Logging/PackTrackerLoggingExtensions.cs`
