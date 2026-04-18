# Updater Flow

## Summary

The updater remains desktop-driven and user-controlled.

The UI still:

- checks for updates
- shows release notes
- asks the user whether to download/install
- lets the user choose when to restart into the update

## Implementation

`PackTracker.Infrastructure.Services.UpdateService` now uses typed `UpdateOptions` instead of hard-coded GitHub constants.

Configurable values:

- `Updates:GitHubOwner`
- `Updates:GitHubRepository`
- `Updates:AllowedAssetExtensions`
- `Updates:UserAgent`
- `Updates:RestartExecutableName`

## Flow

1. Desktop starts and checks GitHub Releases.
2. The updater compares the current version with the latest release tag.
3. The updater selects the first supported asset extension.
4. The user chooses whether to download/install.
5. `.exe` and `.msi` packages are launched elevated.
6. `.zip` packages are extracted by a replacement script that restarts the app afterward.

## Logging

Updater operations log:

- release-check attempts
- selected repository and owner
- download path
- install start/failure
- ZIP extraction failures

## Single-file compatibility

The updater keeps compatibility with single-file desktop publish because restart behavior uses the running executable path or the configured restart executable name.
