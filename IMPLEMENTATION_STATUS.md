# PackTracker Implementation Status

## Situation
PackTracker has a strong high-level architecture and a broad intended feature set, but the current repository appears to be at a mid-implementation stage rather than production-ready.

Observed reality from sampled files:
- The solution structure is sound and aligned to Clean Architecture.
- Core API startup and authentication scaffolding exist.
- The persistence layer contains multiple active business domains.
- The WPF client is wired for DI, hosted services, and navigation.
- Feature maturity is uneven across modules.
- Test coverage is not yet clearly established around critical flows.
- Some architectural coupling exists between Presentation and API.

## Current Assessment

### Overall maturity
- **Architecture:** Good foundation
- **Core wiring:** Present
- **Feature completeness:** Partial
- **Operational readiness:** Unclear / likely incomplete
- **Test readiness:** Low confidence
- **Documentation accuracy:** Aspirational, partially ahead of implementation

## What appears implemented

### API bootstrap
Files reviewed:
- `PackTracker.Api/Program.cs`
- `PackTracker.Api/Controllers/AuthController.cs`

Findings:
- ASP.NET Core API startup is present.
- EF Core PostgreSQL configuration is wired.
- Serilog integration is wired.
- Authentication includes Cookies + Discord OAuth + JWT bearer.
- Authorization policy `HouseWolfOnly` exists.
- Health checks and SignalR are wired.
- Middleware pipeline exists.

### Persistence
Files reviewed:
- `PackTracker.Infrastructure/Persistence/AppDbContext.cs`

Findings:
- DbSets exist for:
  - RefreshTokens
  - Profiles
  - RegolithProfiles
  - RegolithRefineryJobs
  - Commodities
  - CommodityPrices
  - RequestTickets
  - GuideRequests
  - KillEntries
- Entity configuration is present for all sampled models.
- The project is already carrying multiple feature domains.

### Presentation bootstrap
Files reviewed:
- `PackTracker.Presentation/App.xaml.cs`
- `PackTracker.Presentation/PackTracker.Presentation.csproj`

Findings:
- WPF application bootstrap is present.
- DI container is used.
- Logging is configured.
- Hosted API startup from the desktop app is intended.
- Views/ViewModels for welcome, login, dashboard, requests, UEX, settings, and guides are registered.
- SignalR client package is present.

## What appears incomplete or risky

### 1. README is ahead of verified implementation
The README presents a broad and mature platform, but current spot checks do not yet confirm:
- complete end-to-end slices
- validated CI/CD
- robust tests for critical flows
- completed Docker/deployment workflows
- stable module boundaries

### 2. Auth configuration has a confirmed inconsistency risk
`Program.cs` configures Discord OAuth from `settingsService.GetSettings()`.
`AuthController` separately validates raw config keys:
- `Authentication:Discord:ClientId`
- `Authentication:Discord:ClientSecret`

`JwtTokenService` also reads directly from raw config under:
- `Jwt:Key`
- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:ExpiresInMinutes`

Meanwhile, `SettingsService.EnsureBootstrapDefaults(...)` copies configuration values into persistent user settings, creating two overlapping sources of truth.

Risk:
- runtime mismatch between startup configuration and controller/service validation
- false negatives during login flow
- token issuance failures if config and persisted settings drift

### 3. Presentation is tightly coupled to API
`PackTracker.Presentation.csproj` references:
- `PackTracker.Api`
- `PackTracker.Application`
- `PackTracker.Infrastructure`

Risk:
- blurred application boundaries
- harder testing and deployment
- more brittle build graph

### 4. Hosted API boot path still needs verification
`App.xaml.cs` and `LoginView.xaml.cs` both depend on `ApiHostedService` for embedded API startup.
A quick direct path probe did not find the file in the first expected location, which means it is either:
- located elsewhere in the project
- named differently
- missing from the repository

Risk:
- startup path may be incomplete, relocated, or broken
- WPF-hosted API mode needs validation early
- login may fail even if the auth code itself is correct

### 5. Test coverage is not yet trustworthy
Test projects exist, but quick spot checks did not yet reveal obvious critical-path test coverage for auth.

Risk:
- regressions likely while iterating
- authentication and persistence changes may break silently
- login flow may appear implemented without any repeatable verification

## Recommended implementation strategy

## Phase 1 — Full inventory and truth pass
Goal: establish what exists, what builds, and what is incomplete.

Tasks:
1. Inventory controllers, services, entities, ViewModels, and tests.
2. Map each feature domain to one of:
   - scaffold
   - partial
   - usable
   - broken
   - unknown
3. Verify missing or mismatched files referenced in startup paths.
4. Produce a prioritized backlog by operational value and dependency order.

Output:
- This file
- Follow-up inventory refinement
- Concrete prioritized work queue

## Phase 2 — Stabilize one MVP vertical slice
Recommended MVP slice:
- **Discord Auth + Profile bootstrap + WPF login handoff + authenticated `/me`**

Why this first:
- It already has real implementation.
- It is a dependency for most future user-facing features.
- It forces API, DB, tokens, config, and WPF flow to work together.

Definition of done:
- Login endpoint initiates OAuth correctly.
- Callback creates/updates profile.
- Token polling works from client state.
- `/me` returns authenticated user info.
- refresh/logout paths work.
- tests exist around critical paths.

## Phase 3 — Reduce structural risk
Tasks:
1. Normalize auth/config source of truth.
2. Confirm or repair hosted API service placement and startup behavior.
3. Reduce Presentation → API coupling where feasible.
4. Introduce shared DTO/contracts if required.

## Phase 4 — Align documentation to reality
Tasks:
1. Add a `Current Status` section to README.
2. Distinguish implemented vs in-progress vs planned.
3. Remove or fix placeholders that imply unavailable CI/CD or assets.

## Initial priority backlog

### Priority 0 — Repository truth pass
- [ ] Inventory key folders and classes
- [ ] Confirm hosted API service implementation
- [ ] Confirm existing controllers beyond auth
- [ ] Confirm tests and coverage areas
- [ ] Identify which README-promised features actually have endpoints/UI paths

### Priority 1 — Auth slice stabilization
- [ ] Verify settings source for Discord auth
- [ ] Unify config access pattern across `Program`, `AuthController`, `JwtTokenService`, and `SettingsService`
- [ ] Validate OAuth redirect + callback flow
- [ ] Validate token poll flow used by `LoginView`
- [ ] Confirm persisted token encryption/decryption works correctly for WPF client reuse
- [ ] Add tests for auth controller paths
- [ ] Verify `/me`, `refresh`, and `logout` behavior against the WPF client

### Priority 2 — Architectural cleanup
- [ ] Review why WPF references API directly
- [ ] Identify minimum decoupling path
- [ ] Isolate shared contracts if needed

### Priority 3 — Feature slice selection after auth
Candidate next slices:
- Request tickets
- Guide request scheduling/assignment
- Kill tracking ingestion and query flow
- Commodity / UEX data sync

## Recommendation
Proceed immediately with:
1. deeper repo inventory
2. auth slice stabilization
3. documentation truth alignment

## Commander decision point
Unless redirected, the next implementation pass should focus on:
- **Auth + profile + WPF handoff**

That is the highest-leverage route to converting PackTracker from a broad scaffold into a working platform.
