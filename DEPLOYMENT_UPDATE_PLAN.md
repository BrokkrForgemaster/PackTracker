# Deployment, Update, and Windows Trust Plan

## Purpose
Define how PackTracker should support:
- in-app update detection
- user-initiated updates from the UI
- release packaging
- Windows signing/certificate strategy
- reduction of SmartScreen/download warnings

This plan is part of the implementation planning phase because deployment/update decisions influence architecture and user experience.

---

## Product Requirement
The Commander requested:
- an update button in the UI
- users should not need to manually re-download from GitHub each time
- Windows download/execution warnings should be reduced via signing/certificate strategy

This should be treated as a core distribution feature, not an afterthought.

---

## High-Level Recommendation
Use a staged release/update model:

1. **Release packaging pipeline**
   - produce a versioned installer or packaged release artifact

2. **Update manifest endpoint/file**
   - PackTracker checks a trusted release manifest

3. **In-app update notification**
   - UI shows update availability and enables an Update button

4. **User-initiated update flow**
   - download installer/update package from trusted location
   - launch installer/updater

5. **Code signing**
   - sign release artifacts with a valid certificate

---

## Update Experience Recommendation

## Desired UX
When PackTracker starts or on periodic check:
- app checks a release manifest
- if current version < latest version:
  - show update status in the shell/sidebar
  - enable `Update Now`
- user clicks update
- app downloads or launches signed update package
- app exits gracefully and hands off to updater/installer

## UI placement
The existing `UpdateButton` and update status area in `MainWindow` are the right seed location.

Recommended UI states:
- `Up to date`
- `Update available: x.y.z`
- `Downloading update...`
- `Ready to install`
- `Update failed`

---

## Recommended Technical Model

## Option A — Full installer replacement model
App downloads a new installer or updater package and launches it.

### Pros
- straightforward to reason about
- easier for desktop .NET/WPF distribution
- works well with signed installers

### Cons
- less seamless than patch updates

### Recommendation
Best initial implementation choice.

---

## Option B — Self-updating binary swap
App downloads replacement binaries and swaps them in place.

### Pros
- potentially smoother UX

### Cons
- more fragile
- file lock issues
- rollback complexity
- higher risk of bricking installs

### Recommendation
Not recommended for initial implementation.

---

## Option C — External updater framework
Use a proven updater/distribution technology rather than inventing one.

Examples in the Windows ecosystem may include installer/updater frameworks designed for desktop app updates.

### Pros
- less custom update logic
- often better rollback and install behavior

### Cons
- framework adoption cost
- may impose packaging/release constraints

### Recommendation
Evaluate after initial PackTracker release path is stabilized.

---

## Recommended MVP Update Architecture

## Components
### 1. Version source
Application knows its current version from assembly metadata or release metadata.

### 2. Update manifest
A hosted JSON file or API endpoint containing:
- latest version
- release notes summary
- installer download URL
- checksum/hash
- minimum supported version
- release channel (stable/test)
- published timestamp

Example conceptual schema:
```json
{
  "channel": "stable",
  "version": "1.2.0",
  "downloadUrl": "https://example/releases/PackTracker-1.2.0-setup.exe",
  "sha256": "...",
  "notes": "Blueprint explorer and miner requests",
  "publishedAt": "2026-04-03T00:00:00Z"
}
```

### 3. Update checker service
A PackTracker service checks the manifest on startup and optionally on a timer.

### 4. Update download/install coordinator
Handles:
- fetching the installer
- validating checksum
- prompting user if needed
- launching installer/updater
- exiting the app cleanly

### 5. UI integration
MainWindow or settings page surfaces update state and action button.

---

## Recommended Release Hosting Options
- GitHub Releases
- self-hosted HTTPS file server
- object storage with signed/static URLs

### Production preference
Use a stable HTTPS release location controlled by House Wolf or its maintainers.

---

## Windows Trust / Certificate Strategy

## Important reality
A certificate can reduce warnings and build trust, but Windows reputation systems are nuanced.

### Types of signing paths
#### Standard code signing certificate
- signs executables/installers
- improves authenticity and trust
- SmartScreen reputation still builds over time

#### EV code signing certificate
- stronger trust posture
- generally better SmartScreen behavior sooner
- more expensive / stricter issuance

## Recommendation
### Initial practical recommendation
Use a **standard code signing certificate** first if cost-sensitive.

### Best long-term recommendation
Use an **EV code signing certificate** if House Wolf intends broad distribution and wants to minimize SmartScreen friction as much as possible.

---

## Important limitation
No certificate guarantees zero warnings in every situation immediately.
SmartScreen also considers reputation.

But signing is still strongly recommended because it:
- proves publisher identity
- reduces tampering risk
- improves user trust
- improves Windows distribution posture

---

## Packaging Recommendation
For Windows distribution, release as a signed installer package.

Potential approaches:
- signed installer EXE
- signed MSI
- signed bootstrapper + packaged app payload

Initial recommendation:
- produce a versioned signed installer artifact
- update button downloads that artifact
- user launches it from within app flow

---

## Security Requirements for Updates
- manifest must come from trusted HTTPS source
- downloaded installer must be checksum verified
- installer should be signed
- app should not auto-run unsigned payloads
- updater should fail closed if verification fails

---

## MVP Implementation Plan

## Phase 1 — Version & update detection
- add version reporting in app
- create update manifest format
- add update checker service
- display status in UI

## Phase 2 — Update button workflow
- enable `Update Now` when newer version exists
- download signed installer
- verify hash
- launch installer
- prompt user to exit app

## Phase 3 — Release automation
- generate packaged release artifact in build pipeline
- publish manifest alongside release
- attach release notes metadata

## Phase 4 — Signing hardening
- add code signing to release pipeline
- timestamp signatures
- document certificate management process

---

## Suggested UI Behavior
MainWindow sidebar/update panel should show:
- current version
- latest available version
- release notes summary
- update action button when needed
- progress bar during download
- explicit failure message on checksum/signing mismatch

---

## Recommendation Summary
1. finish feature work first enough to justify repeat releases
2. build update manifest + checker
3. wire UI update state into existing shell
4. release via signed installer
5. adopt code signing certificate
6. consider EV certificate if large-scale org distribution is expected

This gives PackTracker a credible internal distribution model without forcing users to manually re-download from GitHub for every update.
