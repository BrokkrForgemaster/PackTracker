using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;

namespace PackTracker.Infrastructure.Services;

/// <summary>
/// Service for checking and installing updates from GitHub releases.
/// </summary>
public class UpdateService : IUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateService> _logger;
    private readonly IVersionService _versionService;
    private readonly UpdateOptions _options;

    public UpdateService(
        HttpClient httpClient,
        ILogger<UpdateService> logger,
        IVersionService versionService,
        IOptions<UpdateOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        }
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Checking GitHub releases for updates. Owner={Owner} Repository={Repository}",
                _options.GitHubOwner,
                _options.GitHubRepository);

            using var response = await _httpClient.GetAsync(GetLatestReleaseApiUrl(), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to check for updates. Status={StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);
            if (release is null)
            {
                _logger.LogWarning("GitHub latest release response could not be parsed.");
                return null;
            }

            var latestVersion = release.TagName?.TrimStart('v') ?? "0.0.0";
            var currentVersion = GetCurrentVersion();

            _logger.LogInformation(
                "Current version={CurrentVersion} Latest version={LatestVersion}",
                currentVersion,
                latestVersion);

            if (!IsNewerVersion(currentVersion, latestVersion))
            {
                _logger.LogInformation("Application is already up to date.");
                return null;
            }

            var installerAsset = release.Assets?.FirstOrDefault(asset =>
                !string.IsNullOrWhiteSpace(asset.Name)
                && _options.AllowedAssetExtensions.Any(extension =>
                    asset.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)));

            if (installerAsset is null || string.IsNullOrWhiteSpace(installerAsset.BrowserDownloadUrl))
            {
                _logger.LogWarning("Latest release did not include a supported installer asset.");
                return null;
            }

            return new UpdateInfo
            {
                Version = latestVersion,
                DownloadUrl = installerAsset.BrowserDownloadUrl,
                ReleaseNotes = release.Body,
                PublishedAt = release.PublishedAt,
                FileSize = installerAsset.Size,
                IsMandatory = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates.");
            return null;
        }
    }

    public async Task<string> DownloadUpdateAsync(
        UpdateInfo updateInfo,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updateInfo);

        try
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "PackTracker", "Updates");
            Directory.CreateDirectory(tempDirectory);

            var fileName = ResolveDownloadFileName(updateInfo.DownloadUrl);
            var filePath = Path.Combine(tempDirectory, fileName);

            // Skip re-download if a previous run already staged this exact installer.
            if (File.Exists(filePath)
                && updateInfo.FileSize is long expectedSize
                && expectedSize > 0
                && new FileInfo(filePath).Length == expectedSize)
            {
                _logger.LogInformation("Reusing previously staged installer at {Path}", filePath);
                progress?.Report(100);
                return filePath;
            }

            _logger.LogInformation("Downloading update from {Url}", updateInfo.DownloadUrl);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            using var response = await _httpClient.GetAsync(
                updateInfo.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes > 0 && progress is not null;

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalBytesRead = 0;

            while (true)
            {
                var bytesRead = await contentStream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;

                if (canReportProgress)
                {
                    progress!.Report((int)((double)totalBytesRead / totalBytes * 100));
                }
            }

            _logger.LogInformation("Update downloaded to {Path}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading update.");
            throw;
        }
    }

    public async Task InstallAndRestartAsync(string installerPath)
    {
        await LaunchInstallerAsync(installerPath);

        Environment.Exit(0);
    }

    public async Task LaunchInstallerAsync(string installerPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installerPath);

        try
        {
            _logger.LogInformation("Launching installer from {Path}", installerPath);

            if (!File.Exists(installerPath))
            {
                throw new FileNotFoundException("Installer file not found.", installerPath);
            }

            var extension = Path.GetExtension(installerPath).ToLowerInvariant();

            switch (extension)
            {
                case ".exe":
                case ".msi":
                    await LaunchInstallerAfterCurrentProcessExitsAsync(installerPath, extension);
                    break;

                case ".zip":
                    await ExtractAndReplaceAsync(installerPath);
                    break;

                default:
                    throw new NotSupportedException($"Installer type '{extension}' is not supported.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error launching installer.");
            throw;
        }
    }

    public string GetCurrentVersion() => _versionService.GetVersion().TrimStart('v');

    private async Task ExtractAndReplaceAsync(string zipPath)
    {
        var applicationDirectory = AppContext.BaseDirectory;
        var extractDirectory = Path.Combine(Path.GetTempPath(), "PackTracker", "Extract", Guid.NewGuid().ToString("N"));

        try
        {
            ZipFile.ExtractToDirectory(zipPath, extractDirectory);

            var restartExecutable = _options.RestartExecutableName;
            if (string.IsNullOrWhiteSpace(restartExecutable))
            {
                restartExecutable = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName);
            }

            var batchScriptPath = Path.Combine(Path.GetTempPath(), "PackTracker_Update.bat");
            var scriptContent = $"""
                                 @echo off
                                 timeout /t 2 /nobreak > nul
                                 echo Updating PackTracker...
                                 xcopy /E /Y /I "{extractDirectory}\*" "{applicationDirectory}"
                                 rmdir /S /Q "{extractDirectory}"
                                 del "{zipPath}"
                                 start "" "{Path.Combine(applicationDirectory, restartExecutable ?? "PackTracker.Presentation.exe")}"
                                 del "%~f0"
                                 """;

            await File.WriteAllTextAsync(batchScriptPath, scriptContent);

            Process.Start(new ProcessStartInfo
            {
                FileName = batchScriptPath,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting ZIP update.");
            throw;
        }
    }

    private void RestartApplication()
    {
        var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            Process.Start(executablePath);
        }

        Environment.Exit(0);
    }

    private string GetLatestReleaseApiUrl() =>
        $"https://api.github.com/repos/{_options.GitHubOwner}/{_options.GitHubRepository}/releases/latest";

    internal static string BuildInstallerBootstrapScript(string installerPath, string extension, int processId)
    {
        // Path and arguments go inside PowerShell single quotes, so escape `'` not `"`.
        var escapedInstallerPath = EscapeForPowerShellSingleQuotedString(installerPath);
        var argumentsList = GetInstallerArgumentsList(extension, escapedInstallerPath);
        var joinedArguments = string.Join(", ", argumentsList.Select(a => $"'{a}'"));
        
        var logPath = Path.Combine(Path.GetTempPath(), "PackTracker", "installer-bootstrap.log");
        var escapedLogPath = EscapeForDoubleQuotedBatchArgument(logPath);

        // For .msi, we launch msiexec.exe explicitly to ensure /i works correctly.
        var targetFilePath = extension == ".msi" ? "msiexec.exe" : escapedInstallerPath;

        return $$"""
                @echo off
                setlocal
                set "LOG={{escapedLogPath}}"
                set "TARGET_PID={{processId}}"
                if not exist "{{Path.GetDirectoryName(logPath)}}" mkdir "{{Path.GetDirectoryName(logPath)}}"
                >>"%LOG%" echo [%date% %time%] bootstrap start, pid=%TARGET_PID%, extension={{extension}}
                
                :wait_for_packtracker_exit
                tasklist /FI "PID eq %TARGET_PID%" 2>NUL | find /I "%TARGET_PID%" >NUL
                if not errorlevel 1 (
                    timeout /t 1 /nobreak >NUL
                    goto wait_for_packtracker_exit
                )
                
                >>"%LOG%" echo [%date% %time%] target exited, invoking PowerShell to launch installer
                >>"%LOG%" echo [%date% %time%] command: Start-Process -FilePath '{{targetFilePath}}' -ArgumentList {{joinedArguments}}
                
                powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command "$p = Start-Process -FilePath '{{targetFilePath}}' -ArgumentList {{joinedArguments}} -Verb RunAs -PassThru -ErrorAction SilentlyContinue; if ($p) { exit 0 } else { exit 1 }"
                
                set "PS_EXIT=%errorlevel%"
                >>"%LOG%" echo [%date% %time%] powershell finished with exit code %PS_EXIT%
                
                if %PS_EXIT% neq 0 (
                    >>"%LOG%" echo [%date% %time%] ERROR: Failed to launch installer (UAC declined or process failed).
                )
                
                del "%~f0"
                """;
    }

    internal static string[] GetInstallerArgumentsList(string extension, string escapedInstallerPath)
    {
        return extension switch
        {
            ".exe" => new[] { "/SILENT", "/CLOSEAPPLICATIONS" },
            ".msi" => new[] { "/i", escapedInstallerPath, "/quiet", "/qn", "/norestart" },
            _ => throw new NotSupportedException($"Installer type '{extension}' is not supported.")
        };
    }

    private async Task LaunchInstallerAfterCurrentProcessExitsAsync(string installerPath, string extension)
    {
        var bootstrapScriptPath = Path.Combine(
            Path.GetTempPath(),
            "PackTracker",
            $"launch-installer-{Guid.NewGuid():N}.cmd");

        Directory.CreateDirectory(Path.GetDirectoryName(bootstrapScriptPath)!);

        var bootstrapScript = BuildInstallerBootstrapScript(
            installerPath,
            extension,
            Environment.ProcessId);

        await File.WriteAllTextAsync(bootstrapScriptPath, bootstrapScript);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{bootstrapScriptPath}\"",
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        _logger.LogInformation(
            "Queued installer bootstrapper at {Path} for extension {Extension}.",
            bootstrapScriptPath,
            extension);
    }

    private static string ResolveDownloadFileName(string downloadUrl)
    {
        if (Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri))
        {
            var fileName = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        return $"packtracker-update-{Guid.NewGuid():N}.bin";
    }

    private static string EscapeForDoubleQuotedBatchArgument(string value) =>
        value.Replace("\"", "\"\"", StringComparison.Ordinal);

    private static string EscapeForPowerShellSingleQuotedString(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);

    private static bool IsNewerVersion(string currentVersion, string newVersion)
    {
        try
        {
            return Version.Parse(newVersion) > Version.Parse(currentVersion);
        }
        catch
        {
            return string.Compare(newVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
        }
    }
}

internal sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public GitHubAsset[]? Assets { get; set; }
}

internal sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
