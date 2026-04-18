# Deployment and Publishing

## Desktop publish target

Validated target:

- Windows x64
- self-contained
- single-file

Command:

```powershell
dotnet publish PackTracker.Presentation -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true
```

Output location:

- `PackTracker.Presentation\bin\Release\net9.0-windows\win-x64\publish\`

## API deployment

The standalone API can still run independently with:

```powershell
dotnet run --project PackTracker.Api
```

For container or cloud deployment, provide configuration via environment variables or mounted configuration, not committed secrets.

## Publish caveats

- `appsettings.json` is copied beside the published app and excluded from single-file bundling so local configuration remains editable.
- The updater remains user-triggered from the desktop shell.
- The embedded API requires valid local settings for database, JWT, and Discord auth when running locally.
