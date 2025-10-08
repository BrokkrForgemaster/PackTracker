using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;

namespace PackTracker.Infrastructure.Services;

/// <summary>
/// Provides persistent, user-scoped app settings storage and change notification.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _userConfigPath;
    private readonly string _defaultConfigPath;
    private readonly FileSystemWatcher _watcher;
    private AppSettings _settings;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userFolder = Path.Combine(appData, "PackTracker");
        Directory.CreateDirectory(userFolder);

        _userConfigPath = Path.Combine(userFolder, "settings.json");

        // Use the executing assembly location as the default config path
        var exeFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                       ?? AppContext.BaseDirectory;
        _defaultConfigPath = Path.Combine(exeFolder, "appsettings.json");

        EnsureUserConfigExists();
        _settings = LoadSettings();

        _watcher = new FileSystemWatcher(userFolder, "settings.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };
        _watcher.Changed += OnUserConfigChanged;
        _watcher.EnableRaisingEvents = true;
    }

    private void EnsureUserConfigExists()
    {
        try
        {
            if (!File.Exists(_userConfigPath))
            {
                if (File.Exists(_defaultConfigPath))
                {
                    File.Copy(_defaultConfigPath, _userConfigPath);
                    _logger.LogInformation("Copied default settings to {Path}", _userConfigPath);
                }
                else
                {
                    var root = new JsonObject
                    {
                        ["AppSettings"] = JsonSerializer.SerializeToNode(
                            new AppSettings(),
                            new JsonSerializerOptions { WriteIndented = true })
                    };
                    File.WriteAllText(
                        _userConfigPath,
                        root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                    _logger.LogInformation("Created blank user settings at {Path}", _userConfigPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user settings");
        }
    }

    private AppSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(_userConfigPath))
            {
                _logger.LogWarning("User config not found at {Path}", _userConfigPath);
                return new AppSettings();
            }

            var json = File.ReadAllText(_userConfigPath);
            var node = JsonNode.Parse(json)?["AppSettings"];
            if (node is null)
            {
                _logger.LogWarning("AppSettings section missing in user config.");
                return new AppSettings();
            }

            var settings = node.Deserialize<AppSettings>() ?? new AppSettings();

            // Decrypt sensitive fields
            settings.ConnectionString = SecretStorage.Unprotect(settings.ConnectionString);
            settings.RegolithApiKey = SecretStorage.Unprotect(settings.RegolithApiKey);
            settings.UexCorpApiKey = SecretStorage.Unprotect(settings.UexCorpApiKey);
            settings.GameLogFilePath = SecretStorage.Unprotect(settings.GameLogFilePath);

            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load user settings");
            return new AppSettings();
        }
    }

    public Task SaveSettings(AppSettings newSettings)
    {
        try
        {
            _logger.LogInformation("Saving user settings to {Path}", _userConfigPath);

            var safeCopy = new AppSettings
            {
                PlayerName = newSettings.PlayerName,
                Theme = newSettings.Theme,
                ConnectionString = SecretStorage.Protect(newSettings.ConnectionString),
                RegolithApiKey = SecretStorage.Protect(newSettings.RegolithApiKey),
                UexCorpApiKey = SecretStorage.Protect(newSettings.UexCorpApiKey),
                GameLogFilePath = SecretStorage.Protect(newSettings.GameLogFilePath),
                DiscordAccessToken = SecretStorage.Protect(newSettings.DiscordAccessToken),
                DiscordRefreshToken = SecretStorage.Protect(newSettings.DiscordRefreshToken),
                JwtToken = SecretStorage.Protect(newSettings.JwtToken),
                JwtRefreshToken = SecretStorage.Protect(newSettings.JwtRefreshToken),
                FirstRunComplete = newSettings.FirstRunComplete,
                UexBaseUrl = newSettings.UexBaseUrl,
                RegolithBaseUrl = newSettings.RegolithBaseUrl
            };

            JsonObject root;
            if (File.Exists(_userConfigPath))
            {
                var text = File.ReadAllText(_userConfigPath);
                root = JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            root["AppSettings"] = JsonSerializer.SerializeToNode(
                safeCopy,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(
                _userConfigPath,
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            _logger.LogInformation("User settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving user settings");
            throw;
        }
        return Task.CompletedTask;
    }

    private void OnUserConfigChanged(object? sender, FileSystemEventArgs e)
    {
        _logger.LogInformation("User settings changed on disk: {Path}", e.FullPath);
        _settings = LoadSettings();
    }

    public AppSettings GetSettings() => _settings;

    public void UpdateSettings(Action<AppSettings> applyUpdates)
    {
        applyUpdates(_settings);
        SaveSettings(_settings);
    }

    public async Task UpdateSettingsAsync(Action<AppSettings> applyUpdates)
    {
        applyUpdates(_settings);
        await Task.Run(() => SaveSettings(_settings));
    }

    public void Dispose()
    {
        _watcher.Dispose();
    }
}
