using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;

namespace PackTracker.Infrastructure.Services;

/// <summary>
/// Provides persistent, user-scoped app settings storage and change notification.
/// </summary>
public sealed class SettingsService : ISettingsService, IDisposable
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _userFolder;
    private readonly string _userConfigPath;
    private readonly string _defaultConfigPath;
    private readonly FileSystemWatcher _watcher;
    private AppSettings _settings = new();

    // The correct blueprint data source — SC Wiki API, free, no key required
    private const string DefaultBlueprintUrl = "https://api.star-citizen.wiki/api/blueprints";

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _userFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HouseWolf",
            "PackTracker"
        );
        Directory.CreateDirectory(_userFolder);

        _userConfigPath = Path.Combine(_userFolder, "user_settings.json");

        var exeFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                        ?? AppContext.BaseDirectory;
        _defaultConfigPath = Path.Combine(exeFolder, "appsettings.json");

        EnsureUserConfigExists();
        _settings = LoadSettings();

        _watcher = new FileSystemWatcher(_userFolder, "user_settings.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };
        _watcher.Changed += OnUserConfigChanged;
        _watcher.EnableRaisingEvents = true;

        _logger.LogInformation("Initialized SettingsService. Config path: {Path}", _userConfigPath);
    }

    // ------------------------------------------------------------------------
    // Initialization
    // ------------------------------------------------------------------------

    private void EnsureUserConfigExists()
    {
        try
        {
            if (File.Exists(_userConfigPath))
                return;

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user settings file");
        }
    }

    // ------------------------------------------------------------------------
    // Load / Save
    // ------------------------------------------------------------------------

    private AppSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(_userConfigPath)) return new AppSettings();

            var json = File.ReadAllText(_userConfigPath);
            if (string.IsNullOrWhiteSpace(json)) return new AppSettings();

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(json);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Settings file corrupted at {Path}. Deleting to recover.", _userConfigPath);
                File.Delete(_userConfigPath);
                return new AppSettings();
            }

            var node = root?["AppSettings"];
            if (node is null) return new AppSettings();

            var settings = node.Deserialize<AppSettings>() ?? new AppSettings();

            // Decrypt sensitive fields
            settings.ConnectionString      = SecretStorage.Unprotect(settings.ConnectionString);
            settings.JwtKey                = SecretStorage.Unprotect(settings.JwtKey);
            settings.DiscordClientId       = SecretStorage.Unprotect(settings.DiscordClientId);
            settings.DiscordClientSecret   = SecretStorage.Unprotect(settings.DiscordClientSecret);
            settings.RegolithApiKey        = SecretStorage.Unprotect(settings.RegolithApiKey);
            settings.UexCorpApiKey         = SecretStorage.Unprotect(settings.UexCorpApiKey);
            settings.GameLogFilePath       = SecretStorage.Unprotect(settings.GameLogFilePath);
            settings.DiscordAccessToken    = SecretStorage.Unprotect(settings.DiscordAccessToken);
            settings.DiscordRefreshToken   = SecretStorage.Unprotect(settings.DiscordRefreshToken);
            settings.JwtToken              = SecretStorage.Unprotect(settings.JwtToken);
            settings.JwtRefreshToken       = SecretStorage.Unprotect(settings.JwtRefreshToken);

            // FIX 1: Correct fallback URL — the old GitHub path doesn't exist
            if (string.IsNullOrWhiteSpace(settings.BlueprintDataSourceUrl) ||
                settings.BlueprintDataSourceUrl.Contains("raw.githubusercontent.com"))
            {
                settings.BlueprintDataSourceUrl = DefaultBlueprintUrl;
            }

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
                PlayerName          = newSettings.PlayerName,
                Theme               = newSettings.Theme,
                FirstRunComplete    = newSettings.FirstRunComplete,

                // FIX 2: BlueprintDataSourceUrl was missing from safeCopy — it got wiped on every save
                BlueprintDataSourceUrl = string.IsNullOrWhiteSpace(newSettings.BlueprintDataSourceUrl)
                    ? DefaultBlueprintUrl
                    : newSettings.BlueprintDataSourceUrl,

                ConnectionString    = SecretStorage.Protect(newSettings.ConnectionString),

                JwtKey              = SecretStorage.Protect(newSettings.JwtKey),
                JwtIssuer           = newSettings.JwtIssuer,
                JwtAudience         = newSettings.JwtAudience,
                JwtExpiresInMinutes = newSettings.JwtExpiresInMinutes,

                DiscordClientId       = SecretStorage.Protect(newSettings.DiscordClientId),
                DiscordClientSecret   = SecretStorage.Protect(newSettings.DiscordClientSecret),
                DiscordCallbackPath   = newSettings.DiscordCallbackPath,
                DiscordRequiredGuildId = newSettings.DiscordRequiredGuildId,
                DiscordConnected      = newSettings.DiscordConnected,
                DiscordAccessToken    = SecretStorage.Protect(newSettings.DiscordAccessToken),
                DiscordRefreshToken   = SecretStorage.Protect(newSettings.DiscordRefreshToken),

                RegolithApiKey  = SecretStorage.Protect(newSettings.RegolithApiKey),
                RegolithBaseUrl = newSettings.RegolithBaseUrl,
                UexCorpApiKey   = SecretStorage.Protect(newSettings.UexCorpApiKey),
                UexBaseUrl      = newSettings.UexBaseUrl,
                ApiBaseUrl      = newSettings.ApiBaseUrl,
                GameLogFilePath = SecretStorage.Protect(newSettings.GameLogFilePath),

                JwtToken        = SecretStorage.Protect(newSettings.JwtToken),
                JwtRefreshToken = SecretStorage.Protect(newSettings.JwtRefreshToken)
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

            // Write atomically
            var tempFile = _userConfigPath + ".tmp";
            File.WriteAllText(tempFile, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tempFile, _userConfigPath, overwrite: true);

            _logger.LogInformation("User settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving user settings");
            throw;
        }

        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------------
    // Updates and Events
    // ------------------------------------------------------------------------

    private void OnUserConfigChanged(object? sender, FileSystemEventArgs e)
    {
        _logger.LogInformation("Detected user settings change: {Path}", e.FullPath);
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

    public void EnsureBootstrapDefaults(IConfiguration configuration)
    {
        if (configuration is null)
            return;

        var changed = false;

        void Assign(ref string target, string? candidate)
        {
            if (string.IsNullOrWhiteSpace(target) && !string.IsNullOrWhiteSpace(candidate))
            {
                target = candidate;
                changed = true;
            }
        }

        var conn = configuration.GetConnectionString("DefaultConnection");
        var connectionString = _settings.ConnectionString;
        Assign(ref connectionString, conn ?? string.Empty);
        _settings.ConnectionString = connectionString;

        var jwtSection = configuration.GetSection("Jwt");
        var jwtKey = _settings.JwtKey;
        Assign(ref jwtKey, jwtSection["Key"]);
        _settings.JwtKey = jwtKey;

        if (!string.IsNullOrWhiteSpace(jwtSection["Issuer"])
            && string.IsNullOrWhiteSpace(_settings.JwtIssuer))
        {
            _settings.JwtIssuer = jwtSection["Issuer"]!;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(jwtSection["Audience"])
            && string.IsNullOrWhiteSpace(_settings.JwtAudience))
        {
            _settings.JwtAudience = jwtSection["Audience"]!;
            changed = true;
        }

        if (_settings.JwtExpiresInMinutes <= 0
            && int.TryParse(jwtSection["ExpiresInMinutes"], out var minutes)
            && minutes > 0)
        {
            _settings.JwtExpiresInMinutes = minutes;
            changed = true;
        }

        var discord = configuration.GetSection("Authentication:Discord");
        var discordClientId = _settings.DiscordClientId;
        Assign(ref discordClientId, discord["ClientId"]);
        _settings.DiscordClientId = discordClientId;

        var discordClientSecret = _settings.DiscordClientSecret;
        Assign(ref discordClientSecret, discord["ClientSecret"]);
        _settings.DiscordClientSecret = discordClientSecret;

        if (string.IsNullOrWhiteSpace(_settings.DiscordCallbackPath))
        {
            var discordCallbackPath = _settings.DiscordCallbackPath;
            Assign(ref discordCallbackPath, discord["CallbackPath"]);
            _settings.DiscordCallbackPath = discordCallbackPath;
        }

        if (string.IsNullOrWhiteSpace(_settings.DiscordCallbackPath))
        {
            _settings.DiscordCallbackPath = "/signin-discord";
            changed = true;
        }

        var discordRequiredGuildId = _settings.DiscordRequiredGuildId;
        Assign(ref discordRequiredGuildId, discord["RequiredGuildId"]);
        _settings.DiscordRequiredGuildId = discordRequiredGuildId;

        var regolithBase = _settings.RegolithBaseUrl;
        Assign(ref regolithBase, configuration["Regolith:BaseUrl"]);
        _settings.RegolithBaseUrl = regolithBase;

        var regolithKey = _settings.RegolithApiKey;
        Assign(ref regolithKey, configuration["Regolith:ApiKey"]);
        _settings.RegolithApiKey = regolithKey;

        var uexBase = _settings.UexBaseUrl;
        Assign(ref uexBase, configuration["Uex:ApiBaseUrl"]);
        _settings.UexBaseUrl = uexBase;

        var uexKey = _settings.UexCorpApiKey;
        Assign(ref uexKey, configuration["Uex:ApiKey"]);
        _settings.UexCorpApiKey = uexKey;

        var apiBase = _settings.ApiBaseUrl;
        Assign(ref apiBase, configuration["Api:BaseUrl"]);
        _settings.ApiBaseUrl = string.IsNullOrWhiteSpace(apiBase) ? "http://localhost:5001" : apiBase;

        // Blueprint URL — bootstrap from config if present, otherwise use SC Wiki default
        var blueprintUrl = _settings.BlueprintDataSourceUrl;
        Assign(ref blueprintUrl, configuration["Blueprints:DataSourceUrl"]);
        if (string.IsNullOrWhiteSpace(blueprintUrl) ||
            blueprintUrl.Contains("raw.githubusercontent.com"))
        {
            blueprintUrl = DefaultBlueprintUrl;
            changed = true;
        }
        _settings.BlueprintDataSourceUrl = blueprintUrl;

        if (string.IsNullOrWhiteSpace(_settings.JwtIssuer))
        {
            _settings.JwtIssuer = "PackTracker";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(_settings.JwtAudience))
        {
            _settings.JwtAudience = "PackTrackerClients";
            changed = true;
        }

        if (_settings.JwtExpiresInMinutes <= 0)
        {
            _settings.JwtExpiresInMinutes = 60;
            changed = true;
        }

        if (changed)
        {
            _logger.LogInformation("Bootstrapping user settings from configuration defaults.");
            SaveSettings(_settings);
        }
    }

    public void Dispose()
    {
        _watcher.Dispose();
    }
}
