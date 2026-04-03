# PackTracker Correct Repo Audit Notes

## Current assessment
This is the correct, substantially implemented PackTracker codebase.

## Confirmed architecture
- ASP.NET Core API with JWT + Discord OAuth
- EF Core persistence using AppDbContext
- WPF desktop client with DI/bootstrap and embedded API host
- SignalR hub for request updates
- UEX integration actively wired
- Regolith integration actively wired
- KillTracker actively wired into presentation and dashboard
- Existing request workflow already implemented in UI and persistence

## Key domain/persistence findings
- Requests persist as `RequestTicket`
- Request SignalR hub exists as `RequestsHub`
- Regolith refinery data persists as `RegolithRefineryJob`
- Guide workflow persists as `GuideRequest`

## Product-direction implications
### Keep and evolve
- Request workflow
- Auth flow
- API + desktop split
- UEX integration (pending deeper review)

### Remove/hide
- KillTracker from product surface
- Regolith / refinery surface due to service shutdown

### Refactor toward
- Crafting requests
- Blueprint visibility / ownership
- Mining support for crafting instead of refinery sync
- Guild coordination / fulfillment workflow

## Current implementation pass
- Sidebar navigation cleanup underway
- Dashboard detached from killtracker + regolith emphasis
- WPF bootstrap cleanup underway
- Next backend target is extending `RequestTicket` / request DTOs for crafting-centric workflows
