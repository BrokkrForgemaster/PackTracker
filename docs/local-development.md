# Local Development

## Prerequisites

- .NET SDK `9.0.304`
- Windows for WPF execution
- PostgreSQL if you intend to run the local embedded API against a local database

## Restore and build

```powershell
dotnet restore PackTracker.sln
dotnet build PackTracker.sln
```

## Run the standalone API

```powershell
dotnet run --project PackTracker.Api
```

## Run the desktop app

```powershell
dotnet run --project PackTracker.Presentation
```

## Embedded API behavior

- If `AppSettings.ApiBaseUrl` points at a remote absolute URL, the desktop app will use that API and skip the embedded host.
- If `ApiBaseUrl` is empty or loopback, the desktop app will start the local embedded API and update the effective loopback URL in user settings.

## Tests

```powershell
dotnet test tests\PackTracker.UnitTests\PackTracker.UnitTests.csproj
dotnet test tests\PackTracker.ApiTests\PackTracker.ApiTests.csproj
dotnet test tests\PackTracker.IntegrationTests\PackTracker.IntegrationTests.csproj
```
