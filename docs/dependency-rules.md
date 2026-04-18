# Dependency Rules

## Allowed references

- `Presentation -> Application, Infrastructure, Api, Logging`
- `Api -> Application, Infrastructure, Logging`
- `Infrastructure -> Application, Domain`
- `Logging -> Application`
- `Application -> Domain`
- `Domain -> none`

## Important constraints

- Domain must stay free of framework and infrastructure concerns.
- Application defines contracts and options, not implementation.
- Infrastructure implements application contracts and persistence.
- API owns HTTP and SignalR concerns only.
- Presentation owns WPF concerns only.
- Logging owns structured logging setup and the `ILoggingService<>` implementation.

## Why `Presentation -> Api` still exists

The desktop client still embeds the local API in-process and reuses the API assembly's controllers, middleware, and hub definitions. The current refactor reduced duplication by centralizing composition in `PackTrackerApiComposition`, but the desktop host still references the API assembly to expose those endpoints.

That dependency is intentional for the embedded-host model and is preferable to duplicating controllers or middleware in the presentation project.
