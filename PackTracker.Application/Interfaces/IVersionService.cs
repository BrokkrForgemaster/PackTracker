namespace PackTracker.Application.Interfaces;

/// <summary>
/// Service for managing application version information.
/// </summary>
public interface IVersionService
{
    /// <summary>
    /// Gets the current application version in format "vX.Y".
    /// </summary>
    string GetVersion();

    /// <summary>
    /// Gets the build date of the application.
    /// </summary>
    string GetBuildDate();

    /// <summary>
    /// Gets the full version string with build date (e.g., "v1.0 (2024-12-01)").
    /// </summary>
    string GetFullVersionString();
}
