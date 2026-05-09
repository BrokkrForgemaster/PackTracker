# PackTracker Engineering Standards

## 1. Cross-Platform Dependency Management
* **Avoid ASP.NET Core in Directory.Build.props**: Do not add `Microsoft.AspNetCore.*` dependencies to the global `Directory.Build.props`. These pull in the ASP.NET Core runtime which is incompatible with Mobile (Android/iOS) targets, causing `NETSDK1082` errors.
* **Surgical Framework References**: Keep `Microsoft.AspNetCore.App` references strictly within the `Api`, `Infrastructure`, and `Logging` projects.

## 2. Mobile & Shared Code Guidelines (CA Rules)
* **CA2007 (ConfigureAwait)**: Always use `.ConfigureAwait(false)` for all awaited tasks in library projects (`Application`, `Domain`, `Infrastructure`) and the `Mobile` project services. This is enforced in the CI/CD pipeline for Release builds.
* **CA1724 (Naming Conflicts)**: The MAUI application class should remain named `App`. If a conflict with `Android.App` occurs, suppress the warning in the code rather than renaming the class.

## 3. Security & Secrets
* **No Hardcoded Tokens**: Never commit Discord Bot Tokens, API Keys, or Connection Strings to `appsettings.json`.
* **Environment Variables**: Use environment variables or a local-only `.env` file for development secrets. The CI/CD pipeline uses GitHub Push Protection to block commits containing secrets.

## 4. Mobile Publication
* **Store Submission**: Refer to `docs/play-store-submission.md` for the Google Play Store checklist and metadata.
* **App Icon**: Use the stylized wolf SVGs in `PackTracker.Mobile/Resources/AppIcon` for all brand assets.
