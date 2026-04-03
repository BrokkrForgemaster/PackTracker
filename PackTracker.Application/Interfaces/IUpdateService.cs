using System;
using System.Threading;
using System.Threading.Tasks;

namespace PackTracker.Application.Interfaces;

/// <summary>
/// Service for checking and installing application updates.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Checks if a new version is available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Update info if available, null otherwise</returns>
    Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the specified update.
    /// </summary>
    /// <param name="updateInfo">Update information</param>
    /// <param name="progress">Progress callback (0-100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to file</returns>
    Task<string> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs the downloaded update and restarts the application.
    /// </summary>
    /// <param name="installerPath">Path to installer file</param>
    Task InstallAndRestartAsync(string installerPath);

    /// <summary>
    /// Gets the current application version.
    /// </summary>
    string GetCurrentVersion();
}

/// <summary>
/// Information about an available update.
/// </summary>
public record UpdateInfo
{
    /// <summary>
    /// Version string (e.g., "1.1.0")
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Download URL for the installer
    /// </summary>
    public required string DownloadUrl { get; init; }

    /// <summary>
    /// Release notes/changelog
    /// </summary>
    public string? ReleaseNotes { get; init; }

    /// <summary>
    /// Release date
    /// </summary>
    public DateTime? PublishedAt { get; init; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long? FileSize { get; init; }

    /// <summary>
    /// Whether this is a mandatory update
    /// </summary>
    public bool IsMandatory { get; init; }
}
