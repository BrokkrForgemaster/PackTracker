# Mobile Platform Readiness

## Overview

PackTracker has been prepared for Android, iOS, and Mac Catalyst deployment via .NET MAUI while preserving the existing WPF desktop implementation.

## Android Readiness

- **Status:** Project scaffolded and configured
- **Package ID:** `com.housewolf.packtracker`
- **Min SDK:** API 21 (Android 5.0)
- **Target SDK:** API 34
- **Permissions:** `INTERNET` only
- **Build target:** `net9.0-android`
- **Readiness:** Project builds; UI scaffolded; backend integration pending

### Google Play Notes
- ApplicationId (`com.housewolf.packtracker`) must be unique in Play Store
- Signing: add release keystore and signing config before submitting
- App icon provided as SVG; replace with production artwork
- Target SDK 34 satisfies current Play Store requirements

## iOS Readiness

- **Status:** Project scaffolded and configured
- **Bundle ID:** `com.housewolf.packtracker`
- **Min iOS:** 15.0
- **Build target:** `net9.0-ios`
- **Readiness:** Project configured; requires macOS + Xcode to build

### Apple Readiness Notes
- Requires Apple Developer account for distribution
- No privacy-sensitive entitlements requested
- NSAppTransportSecurity set to allow local networking for embedded API
- Signing and provisioning profiles required before TestFlight/App Store submission

## Mac Catalyst Readiness

- **Status:** Project scaffolded
- **Build target:** `net9.0-maccatalyst`
- **Min macOS:** macOS 12 (Monterey, via Catalyst 15.0)

## Responsive Breakpoint Rules

| Breakpoint | Width Range     | Navigation       | Content Columns |
|------------|-----------------|------------------|-----------------|
| Compact    | < 600px         | Drawer / Bottom  | 1               |
| Medium     | 600px – 1023px  | Drawer           | 1               |
| Expanded   | >= 1024px       | Sidebar          | 2               |

## Known Unsupported Views (Mobile)

The following WPF views have no direct MAUI equivalent yet:

- `ComponentView.xaml` — Blueprint component detail
- `HelpView.xaml` — In-app help overlay
- `WelcomeView.xaml` — First-run welcome
- `RecruitmentView.xaml` — Recruitment flow
- `UexView.xaml` — UEX trading data viewer
- All Admin sub-views (AdminNominationsView, AdminMedalsView, etc.)
- All dialog windows (NewRequestDialog, CraftingRequestFormDialog, etc.)

## Auth Flow

Discord OAuth -> backend token -> `SecureStorage` (MAUI) -> authenticated API calls.
Tokens are never stored in plaintext or preferences.

## Testing Commands

```bash
# Restore and build all projects
dotnet restore
dotnet build

# Run responsive tests
dotnet test tests/PackTracker.Responsive.Tests/PackTracker.Responsive.Tests.csproj

# Run mobile readiness tests
dotnet test tests/PackTracker.Mobile.Tests/PackTracker.Mobile.Tests.csproj

# Build Android target (requires Android SDK / MAUI workload)
dotnet build PackTracker.Mobile/PackTracker.Mobile.csproj -f net9.0-android

# Build iOS (requires macOS + Xcode)
# dotnet build PackTracker.Mobile/PackTracker.Mobile.csproj -f net9.0-ios

# Build Mac Catalyst (requires macOS + Xcode)
# dotnet build PackTracker.Mobile/PackTracker.Mobile.csproj -f net9.0-maccatalyst
```

## Remaining Mobile Polish

1. Wire ViewModels to MAUI pages (reuse existing Application layer)
2. Implement Discord OAuth flow using `WebAuthenticator`
3. Connect `ITokenStorage` to API authentication headers
4. Implement real data binding for Dashboard, RequestHub, Blueprints
5. Add pull-to-refresh on list pages
6. Add offline/error state handling
7. Replace placeholder SVG icons with production assets
8. Add push notification support (`POST_NOTIFICATIONS`) when notification service is implemented
9. Test on physical Android device and iPad
10. Complete Admin pages for admin users on mobile
