---
description: Refactor an existing .NET 9 WPF + embedded Web API solution into production-grade Clean Architecture based on the amantinband clean-architecture reference, while preserving user-facing behavior and avoiding unnecessary UI visual changes.
argument-hint: [REPO_URL] [OPTIONAL_FOCUS="full-refactor"]
---
You are a senior .NET 9 architect and lead engineer.

Your task is to refactor an existing repository into a proper, production-grade Clean Architecture solution, using the concepts and quality bar of this reference repository as the architectural model:

Reference Architecture:
https://github.com/amantinband/clean-architecture

Target Repository to Refactor:
$1

Optional focus:
$2

If no repository URL is provided, stop and ask for it before continuing.

## Mission

Refactor the target solution into a modern Clean Architecture implementation for .NET 9, while preserving the current user-facing behavior and overall functionality, and improving maintainability, testability, structured logging, updating, and deployment quality.

The expected target is a Desktop WPF application with an embedded/local Web API unless the repository clearly proves otherwise. If the repo differs, adapt the plan to the actual app shape, but still follow the same architectural goals.

Your output must be a fully working codebase, not just advice.

## Non-Negotiable Rules

### 1. Preserve user-facing behavior
- The current front-facing behavior must remain the same or improve.
- Existing workflows should continue to work.
- If an internal redesign is needed, keep external behavior compatible unless there is a clear user-experience improvement.

### 2. Preserve the embedded API model
- Keep support for the current model where the desktop app can run or use the local embedded API.
- You may improve its structure and startup design.
- You may make the API separately runnable if helpful, but the default desktop experience must still support local embedded use.

### 3. Do not casually change UI visuals
- Do not refactor the WPF UI visual layout just for style.
- Visual updates are allowed only if they are justified first with valid reasoning.
- Functional refactors to code-behind, ViewModels, bindings, DI, commands, navigation, validation, and presentation-layer structure are allowed and encouraged.
- If you propose visual modernization, clearly explain:
  - what is changing,
  - why it is justified,
  - why it improves maintainability or usability,
  - and how it avoids breaking the user experience.

### 4. Keep or improve updater behavior
- If the current app checks GitHub Releases or another remote source for updates, preserve that capability or improve it.
- You may redesign the updater architecture.
- The result must still support update delivery in a clean, resilient way.
- The user must still be able to choose when to restart or apply the update.
- Preserve single-file publish compatibility.

### 5. Publish requirements
- Optimize for Windows x64 single-file self-contained publish unless the repo clearly requires a different publish target.
- If you identify a more seamless deployment or update option, you may implement it, but you must:
  - explain why it is better,
  - preserve user-controlled restart or update timing,
  - preserve current update expectations,
  - and keep deployment simple.

### 6. Logging must be extracted
- Extract logging into its own dedicated project, such as:
  - `*.Logging`
- This project should own structured logging concerns, including:
  - logging configuration,
  - Serilog bootstrap,
  - enrichers,
  - correlation IDs,
  - exception logging,
  - sink setup,
  - audit or event logging,
  - shared logging abstractions,
  - reusable logging helpers or extensions.
- Logging must be modern, structured, and production-ready.

### 7. Preserve current providers and integrations
- Preserve the current database provider strategy and schema support.
- If the project currently supports multiple providers, retain them where applicable.
- Preserve front-facing authentication behavior, even if you refactor internals.
- You may restructure auth implementation, token handling, or startup flow, but the user experience must stay the same or improve.

### 8. Architecture and patterns
Use modern patterns where appropriate, including:
- Clean Architecture
- CQRS
- MediatR
- FluentValidation
- repository pattern only where it adds value
- Unit of Work where appropriate
- domain events
- background services
- resilient infrastructure services
- Result or Error pattern
- dependency inversion
- feature or vertical-slice organization where useful
- proper options or configuration binding
- retry or resilience patterns
- structured logging
- separation of concerns
- testable application services

Use modern patterns only where they add real value. Do not add complexity just to check a box.

### 9. Testing policy
- First priority: make the project work.
- After functionality is stable, add full unit tests.
- Integration tests are optional, not required for completion.
- Do not block completion on integration test setup.

### 10. Documentation is required
Generate fully documented project docs, including:
- `README.md`
- architecture overview
- dependency rules
- local setup guide
- deployment or publishing guide
- updater flow guide
- secrets or configuration guide
- refactor report
- any additional docs needed to onboard a new developer quickly

### 11. No secrets exposed
- No secrets may remain hard-coded in source.
- Actively inspect for sensitive values and replace them with secure configuration.
- Use safe configuration practices:
  - environment variables,
  - user secrets where appropriate,
  - secure options classes,
  - placeholder config templates,
  - `.env.example` or equivalent,
  - `appsettings.*.example.json` if helpful.
- Remove or neutralize any leaked secrets you find.

### 12. API compatibility
- Preserve current public API contracts where possible.
- You may change or version endpoints only if doing so clearly improves the user experience or reliability.
- Avoid unnecessary breaking changes.

## Expected Architecture Target

Refactor the solution toward a structure conceptually aligned with the reference architecture, adapted for this WPF plus embedded API system.

Use or refine projects along these lines:
- `*.Domain`
- `*.Application`
- `*.Infrastructure`
- `*.Api`
- `*.Presentation` or the WPF UI project
- `*.Logging`
- `*.Contracts` or shared abstractions or contracts if justified
- `*.Tests`

You may adjust exact names if needed, but keep the structure clean and intentional.

## Layer Responsibilities

### Domain
Contains:
- entities
- value objects
- enums
- domain services
- domain rules
- domain events
- pure business logic
- no infrastructure dependencies

### Application
Contains:
- use cases
- commands and queries
- handlers
- validators
- interfaces
- DTOs where appropriate
- orchestration logic
- transaction boundaries where appropriate
- no infrastructure implementation details

### Infrastructure
Contains:
- EF Core
- repositories if needed
- external APIs
- file system services
- auth infrastructure
- updater infrastructure
- persistence
- background jobs or services
- configuration wiring
- implementations of application interfaces

### API
Contains:
- HTTP endpoints, controllers, or minimal APIs
- API composition root
- auth pipeline
- request or response mapping
- API-specific middleware
- embedded host support

### Presentation
Contains:
- views
- ViewModels
- bindings
- commands
- navigation logic
- presentation-specific adapters
- no business logic that belongs in Application or Domain

### Logging
Contains:
- Serilog configuration
- log enrichers
- correlation and context
- exception formatting
- sink registration
- structured logging policy
- logging abstractions or helpers

## Required Refactor Strategy

Follow this sequence.

### Phase 1 - Analyze
1. Inspect the full repository.
2. Produce a dependency map of the current solution.
3. Identify architectural violations, including:
   - business logic in UI
   - business logic in infrastructure
   - direct data access from presentation
   - cross-layer leakage
   - tightly coupled services
   - startup or config confusion
   - auth concerns in wrong layers
   - updater logic mixed into UI or infrastructure improperly
   - logging concerns spread across projects
4. Identify all current functionality that must be preserved.

### Phase 2 - Plan
Produce a concise refactor plan before changing code. It must include:
- current state summary
- target state summary
- project-by-project responsibilities
- known risks
- compatibility constraints
- rollout order
- any UI changes proposed, with explicit justification

### Phase 3 - Refactor architecture
Refactor the codebase to enforce clean boundaries.

Required outcomes:
- domain remains pure
- application owns use-case orchestration
- infrastructure implements external dependencies
- WPF presentation becomes thin and testable
- API becomes a proper host or composition boundary
- logging is extracted cleanly
- configuration is strongly typed
- secrets are removed from code
- startup or bootstrap is simplified and reliable

### Phase 4 - Embedded API and hosting
Refactor the embedded or local API hosting so it is robust and maintainable.

Goals:
- WPF app can reliably start or connect to the local API
- startup lifecycle is clean
- failures are logged clearly
- cancellation and shutdown are graceful
- host composition is easy to understand
- no fragile hidden coupling

### Phase 5 - Updater redesign
Refactor the updater into a clean service architecture.

Requirements:
- continue supporting update retrieval from GitHub Releases or a superior equivalent
- preserve single-file deployment compatibility
- let the user decide when to restart or apply update
- ensure update flow is resilient, logged, and testable
- separate updater orchestration from UI details

### Phase 6 - Logging extraction
Create a dedicated logging project and move all logging concerns there.

Requirements:
- use structured logging everywhere
- use contextual properties and enrichment
- ensure exceptions are logged with useful detail
- ensure logs are meaningful for desktop, API, and updater workflows
- avoid inconsistent logging

### Phase 7 - Harden auth and configuration
Refactor auth and config internals while preserving front-facing behavior.

Requirements:
- strongly typed options
- no secrets in code
- clear token or auth handling
- configuration validation where appropriate
- minimal startup ambiguity

### Phase 8 - Make it build and run end-to-end
After refactor:
- solution must restore
- solution must build
- desktop app must run
- local or embedded API must run
- key workflows must function
- updater flow must remain viable
- single-file publish must be validated

### Phase 9 - Unit tests
After the project works:
- add meaningful unit tests for domain and application logic
- add tests for validators, handlers, mapping, and critical services where practical
- focus on correctness, not shallow coverage

### Phase 10 - Documentation
Generate or update docs so another developer can understand and maintain the solution.

## Technical Preferences

You may introduce or standardize these where useful:
- MediatR
- FluentValidation
- Mapster or AutoMapper
- ErrorOr or FluentResults or an equivalent Result pattern
- Scrutor
- Polly
- OpenTelemetry if it adds value
- options pattern
- typed HttpClient
- hosted services or background workers where justified

Prefer:
- constructor injection
- immutable request or response models when reasonable
- clear interfaces at application boundaries
- feature or vertical-slice organization where it improves clarity

Do not:
- introduce pointless abstractions
- over-genericize everything
- create repository or unit-of-work layers that add no value
- duplicate DTOs unnecessarily
- hide logic in frameworks

## UI Constraints

The WPF UI may be reworked internally.

Allowed:
- move logic out of code-behind
- improve ViewModels
- improve binding or command patterns
- improve validation flow
- improve navigation or state handling
- improve DI and composition
- improve async behavior and cancellation
- improve maintainability and testability

Not allowed without explicit justification:
- arbitrary visual redesign
- changing the look and feel just because
- changing user workflows without good reason

If visual changes are proposed:
1. explain them first,
2. justify them,
3. keep them minimal,
4. preserve user familiarity.

## Code Quality Requirements

All code you produce must be:
- production-ready
- idiomatic modern .NET 9
- null-safe
- async where appropriate
- cancellation-aware where appropriate
- thoroughly error-handled
- readable
- consistent
- documented where needed

Also ensure:
- no dead code left behind
- no obsolete temp scaffolding
- no hidden magic strings if avoidable
- no secrets committed
- minimal startup ambiguity
- strong naming
- clear folder structure

## Deliverables

You must produce all of the following:

### 1. Refactored codebase
A working refactored solution.

### 2. Refactor report
A markdown report containing:
- major issues found
- changes made
- architectural decisions
- compatibility notes
- known tradeoffs
- remaining recommendations

### 3. Documentation set
At minimum:
- `README.md`
- `docs/architecture.md`
- `docs/dependency-rules.md`
- `docs/local-development.md`
- `docs/deployment.md`
- `docs/updater-flow.md`
- `docs/configuration-and-secrets.md`
- `docs/refactor-report.md`

### 4. Publish validation
Document how to publish the app as:
- Windows x64
- single-file
- self-contained

Include any caveats for updater compatibility.

### 5. Unit tests
A meaningful unit test suite for the refactored logic.

## Acceptance Criteria

The task is complete only when all of the following are true:

1. The solution builds successfully.
2. The WPF application runs.
3. The embedded or local API model still works.
4. Front-facing behavior is preserved or improved.
5. UI visuals are not arbitrarily changed.
6. Logging is extracted into its own project and is structured.
7. Secrets are removed from code and replaced with secure config patterns.
8. The updater flow remains viable and user-controlled.
9. The app can be published as Windows x64 single-file self-contained.
10. Unit tests are added after the project is functioning.
11. Documentation is complete and useful.
12. The architecture clearly reflects Clean Architecture principles aligned to the reference repo.

## Execution Rules

While performing the refactor:
- Do not stop at analysis.
- Do not only suggest changes.
- Implement them.
- Make decisions autonomously when reasonable.
- Minimize human interaction.
- If you must choose between elegance and preserving working behavior, preserve working behavior first.
- If you identify uncertainty, use the safest option that preserves behavior.
- Explain major decisions in the refactor report, not through constant user interruption.

When making tradeoffs:
- prefer maintainability,
- prefer clean boundaries,
- prefer user experience,
- prefer reliable deployment,
- prefer secure configuration,
- prefer clarity over cleverness.

## Final Output Format

Provide your work in this order:
1. Repository assessment
2. Refactor plan
3. Implemented architectural changes
4. Logging extraction summary
5. Updater redesign summary
6. Secrets and configuration remediation summary
7. Build, run, and publish validation
8. Unit testing summary
9. Documentation summary
10. Open issues or recommendations

Then provide the actual code changes and documentation updates.

## Optional Focus Handling

If `$2` is provided, treat it as a priority area without ignoring the full mission. Examples:
- `logging`
- `updater`
- `auth`
- `api-hosting`
- `testing`
- `publish`
- `full-refactor`

Default to `full-refactor` if omitted.