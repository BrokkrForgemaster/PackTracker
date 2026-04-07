using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;

namespace PackTracker.Infrastructure.Services;

/// <summary>
/// Service for checking and installing updates from GitHub releases.
/// </summary>
/// <remarks>
/// Checks GitHub releases for newer versions and downloads installers.
/// Supports progress reporting and cancellation.
/// </remarks>
public class UpdateService : IUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateService> _logger;
    private readonly IVersionService _versionService;

    // Configure your GitHub repository
    private const string GitHubOwner = "BrokkrForgemaster";
    private const string GitHubRepo = "PackTracker";
    private const string GitHubApiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

    public UpdateService(
        IHttpClientFactory httpClientFactory,
        ILogger<UpdateService> logger,
        IVersionService versionService)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "PackTracker-Updater");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
    }

    /// <summary>
    /// Checks GitHub releases for a newer version.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking for updates from GitHub...");

            var response = await _httpClient.GetAsync(GitHubApiUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to check for updates. Status: {Status}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);

            if (release == null)
            {
                _logger.LogWarning("Failed to parse GitHub release JSON");
                return null;
            }

            // Parse version (remove 'v' prefix if present)
            var latestVersion = release.TagName?.TrimStart('v') ?? "0.0.0";
            var currentVersion = GetCurrentVersion();

            _logger.LogInformation("Current version: {Current}, Latest version: {Latest}",
                currentVersion, latestVersion);

            // Compare versions
            if (!IsNewerVersion(currentVersion, latestVersion))
            {
                _logger.LogInformation("Application is up to date");
                return null;
            }

            // Find installer asset (look for .exe, .msi, or .zip)
            var installerAsset = release.Assets?
                .FirstOrDefault(a => a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true ||
                                     a.Name?.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) == true ||
                                     a.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true);

            if (installerAsset == null)
            {
                _logger.LogWarning("No installer found in latest release");
                return null;
            }

            var updateInfo = new UpdateInfo
            {
                Version = latestVersion,
                DownloadUrl = installerAsset.BrowserDownloadUrl ?? string.Empty,
                ReleaseNotes = release.Body,
                PublishedAt = release.PublishedAt,
                FileSize = installerAsset.Size,
                IsMandatory = false // Could parse this from release notes
            };

            _logger.LogInformation("Update available: {Version}", updateInfo.Version);
            return updateInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            return null;
        }
    }

    /// <summary>
    /// Downloads the update installer with progress reporting.
    /// </summary>
    public async Task<string> DownloadUpdateAsync(
        UpdateInfo updateInfo,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Downloading update from {Url}", updateInfo.DownloadUrl);

            // Create temp directory for downloads
            var tempDir = Path.Combine(Path.GetTempPath(), "PackTracker", "Updates");
            Directory.CreateDirectory(tempDir);

            // Generate filename
            var fileName = Path.GetFileName(new Uri(updateInfo.DownloadUrl).LocalPath);
            var filePath = Path.Combine(tempDir, fileName);

            // Delete existing file if present
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // Download with progress
            using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes > 0 && progress != null;

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write,
                FileShare.None, 8192, true);

            var buffer = new byte[8192];
            var totalBytesRead = 0L;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalBytesRead += bytesRead;

                if (canReportProgress)
                {
                    var progressPercentage = (int)((double)totalBytesRead / totalBytes * 100);
                    progress?.Report(progressPercentage);
                }
            }

            _logger.LogInformation("Update downloaded to {Path}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading update");
            throw;
        }
    }

    /// <summary>
    /// Installs the update and restarts the application.
    /// </summary>
    public async Task InstallAndRestartAsync(string installerPath)
    {
        try
        {
            _logger.LogInformation("Installing update from {Path}", installerPath);

            if (!File.Exists(installerPath))
            {
                throw new FileNotFoundException("Installer file not found", installerPath);
            }

            // Determine installer type
            var extension = Path.GetExtension(installerPath).ToLowerInvariant();

            ProcessStartInfo startInfo;

            switch (extension)
            {
                case ".exe":
                    // Execute installer with silent install flags (customize as needed)
                    startInfo = new ProcessStartInfo
                    {
                        FileName = installerPath,
                        Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                        UseShellExecute = true,
                        Verb = "runas" // Run as administrator
                    };
                    break;

                case ".msi":
                    // MSI installer
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "msiexec.exe",
                        Arguments = $"/i \"{installerPath}\" /quiet /qn /norestart",
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    break;

                case ".zip":
                    // For ZIP files, extract and replace application files
                    await ExtractAndReplaceAsync(installerPath);
                    RestartApplication();
                    return;

                default:
                    throw new NotSupportedException($"Installer type '{extension}' not supported");
            }

            // Start installer
            Process.Start(startInfo);

            _logger.LogInformation("Installer started. Shutting down application...");

            // Give installer time to start
            await Task.Delay(1000);

            // Exit application to allow installer to proceed
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing update");
            throw;
        }
    }

    /// <summary>
    /// Extracts ZIP update and replaces application files.
    /// </summary>
    private async Task ExtractAndReplaceAsync(string zipPath)
    {
        var appDir = AppContext.BaseDirectory;
        var tempExtractDir = Path.Combine(Path.GetTempPath(), "PackTracker", "Extract", Guid.NewGuid().ToString());

        try
        {
            // Extract to temp directory
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempExtractDir);

            // Create batch script to replace files after app exits
            var batchScript = Path.Combine(Path.GetTempPath(), "PackTracker_Update.bat");
            var scriptContent = $@"
@echo off
timeout /t 2 /nobreak > nul
echo Updating PackTracker...
xcopy /E /Y /I ""{tempExtractDir}\*"" ""{appDir}""
rmdir /S /Q ""{tempExtractDir}""
del ""{zipPath}""
start """" ""{Path.Combine(appDir, "PackTracker.Presentation.exe")}""
del ""%~f0""
";
            await File.WriteAllTextAsync(batchScript, scriptContent);

            // Start batch script
            Process.Start(new ProcessStartInfo
            {
                FileName = batchScript,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting update");
            throw;
        }
    }

    /// <summary>
    /// Restarts the application.
    /// </summary>
    private void RestartApplication()
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (exePath != null)
        {
            Process.Start(exePath);
        }
        Environment.Exit(0);
    }

    /// <summary>
    /// Gets the current application version.
    /// </summary>
    public string GetCurrentVersion()
    {
        return _versionService.GetVersion().TrimStart('v');
    }

    /// <summary>
    /// Compares version strings to determine if new version is newer.
    /// </summary>
    private bool IsNewerVersion(string currentVersion, string newVersion)
    {
        try
        {
            var current = Version.Parse(currentVersion);
            var latest = Version.Parse(newVersion);
            return latest > current;
        }
        catch
        {
            // Fallback to string comparison
            return string.Compare(newVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
        }
    }
}

#region GitHub API Models

internal class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public GitHubAsset[]? Assets { get; set; }
}

internal class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

#endregion
