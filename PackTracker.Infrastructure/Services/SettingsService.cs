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
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    private readonly ILogger<SettingsService> _logger;
    private readonly object _settingsSync = new();
    private readonly string _userFolder;
    private readonly string _userConfigPath;
    private readonly string _defaultConfigPath;
    private readonly FileSystemWatcher _watcher;
    private AppSettings _settings = new();
    private bool _suspendWatcherReload;

    private const string DefaultBlueprintUrl = "https://api.star-citizen.wiki/api/blueprints";

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _userFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HouseWolf",
            "PackTracker");
        Directory.CreateDirectory(_userFolder);

        _userConfigPath = Path.Combine(_userFolder, "user_settings.json");

        var exeFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                        ?? AppContext.BaseDirectory;
        _defaultConfigPath = Path.Combine(exeFolder, "appsettings.json");

        lock (_settingsSync)
        {
            EnsureUserConfigExists();
            _settings = LoadSettingsCore();
        }

        _watcher = new FileSystemWatcher(_userFolder, "user_settings.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };
        _watcher.Changed += OnUserConfigChanged;
        _watcher.EnableRaisingEvents = true;

        _logger.LogInformation("Initialized SettingsService. Config path: {Path}", _userConfigPath);
    }

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
                        IndentedOptions)
                };
                File.WriteAllText(
                    _userConfigPath,
                    root.ToJsonString(IndentedOptions));
                _logger.LogInformation("Created blank user settings at {Path}", _userConfigPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user settings file");
        }
    }

    private AppSettings LoadSettingsCore()
    {
        try
        {
            if (!File.Exists(_userConfigPath))
                return new AppSettings();

            var json = File.ReadAllText(_userConfigPath);
            if (string.IsNullOrWhiteSpace(json))
                return new AppSettings();

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
            if (node is null)
                return new AppSettings();

            var settings = node.Deserialize<AppSettings>() ?? new AppSettings();

            settings.ConnectionString = SecretStorage.Unprotect(settings.ConnectionString);
            settings.ApiBaseUrl = string.IsNullOrWhiteSpace(settings.ApiBaseUrl) ? string.Empty : settings.ApiBaseUrl.TrimEnd('/');
            settings.JwtKey = SecretStorage.Unprotect(settings.JwtKey);
            settings.DiscordClientId = SecretStorage.Unprotect(settings.DiscordClientId);
            settings.DiscordClientSecret = SecretStorage.Unprotect(settings.DiscordClientSecret);
            settings.DiscordBotToken = SecretStorage.Unprotect(settings.DiscordBotToken);
            settings.RegolithApiKey = SecretStorage.Unprotect(settings.RegolithApiKey);
            settings.UexCorpApiKey = SecretStorage.Unprotect(settings.UexCorpApiKey);
            settings.GameLogFilePath = SecretStorage.Unprotect(settings.GameLogFilePath);
            settings.DiscordAccessToken = SecretStorage.Unprotect(settings.DiscordAccessToken);
            settings.DiscordRefreshToken = SecretStorage.Unprotect(settings.DiscordRefreshToken);
            settings.JwtToken = SecretStorage.Unprotect(settings.JwtToken);
            settings.JwtRefreshToken = SecretStorage.Unprotect(settings.JwtRefreshToken);

            return NormalizeSettings(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load user settings");
            return new AppSettings();
        }
    }

    public Task SaveSettings(AppSettings newSettings)
    {
        lock (_settingsSync)
        {
            SaveSettingsCore(newSettings);
        }

        return Task.CompletedTask;
    }

    private void OnUserConfigChanged(object? sender, FileSystemEventArgs e)
    {
        lock (_settingsSync)
        {
            if (_suspendWatcherReload)
                return;

            _logger.LogInformation("Detected user settings change: {Path}", e.FullPath);
            _settings = LoadSettingsCore();
        }
    }

    public AppSettings GetSettings()
    {
        lock (_settingsSync)
        {
            return CloneSettings(_settings);
        }
    }

    public void UpdateSettings(Action<AppSettings> applyUpdates)
    {
        ArgumentNullException.ThrowIfNull(applyUpdates);

        lock (_settingsSync)
        {
            var updatedSettings = CloneSettings(_settings);
            applyUpdates(updatedSettings);
            SaveSettingsCore(updatedSettings);
        }
    }

    public Task UpdateSettingsAsync(Action<AppSettings> applyUpdates)
    {
        ArgumentNullException.ThrowIfNull(applyUpdates);
        return Task.Run(() => UpdateSettings(applyUpdates));
    }

    public void EnsureBootstrapDefaults(IConfiguration configuration)
    {
        if (configuration is null)
            return;

        lock (_settingsSync)
        {
            var changed = false;
            var updatedSettings = CloneSettings(_settings);

            void Assign(ref string target, string? candidate)
            {
                if (string.IsNullOrWhiteSpace(target) && !string.IsNullOrWhiteSpace(candidate))
                {
                    target = candidate;
                    changed = true;
                }
            }

            var connectionString = updatedSettings.ConnectionString;
            Assign(ref connectionString, configuration.GetConnectionString("DefaultConnection"));
            updatedSettings.ConnectionString = connectionString;

            var jwtSection = configuration.GetSection("Authentication:Jwt");
            var jwtKey = updatedSettings.JwtKey;
            Assign(ref jwtKey, jwtSection["Key"]);
            updatedSettings.JwtKey = jwtKey;

            if (!string.IsNullOrWhiteSpace(jwtSection["Issuer"]) && string.IsNullOrWhiteSpace(updatedSettings.JwtIssuer))
            {
                updatedSettings.JwtIssuer = jwtSection["Issuer"]!;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(jwtSection["Audience"]) && string.IsNullOrWhiteSpace(updatedSettings.JwtAudience))
            {
                updatedSettings.JwtAudience = jwtSection["Audience"]!;
                changed = true;
            }

            if (updatedSettings.JwtExpiresInMinutes <= 0
                && int.TryParse(jwtSection["ExpiresInMinutes"], out var minutes)
                && minutes > 0)
            {
                updatedSettings.JwtExpiresInMinutes = minutes;
                changed = true;
            }

            var discord = configuration.GetSection("Authentication:Discord");
            var discordClientId = updatedSettings.DiscordClientId;
            Assign(ref discordClientId, discord["ClientId"]);
            updatedSettings.DiscordClientId = discordClientId;

            var discordClientSecret = updatedSettings.DiscordClientSecret;
            Assign(ref discordClientSecret, discord["ClientSecret"]);
            updatedSettings.DiscordClientSecret = discordClientSecret;

            if (string.IsNullOrWhiteSpace(updatedSettings.DiscordCallbackPath))
            {
                var discordCallbackPath = updatedSettings.DiscordCallbackPath;
                Assign(ref discordCallbackPath, discord["CallbackPath"]);
                updatedSettings.DiscordCallbackPath = discordCallbackPath;
            }

            if (string.IsNullOrWhiteSpace(updatedSettings.DiscordCallbackPath))
            {
                updatedSettings.DiscordCallbackPath = "/signin-discord";
                changed = true;
            }

            var discordRequiredGuildId = updatedSettings.DiscordRequiredGuildId;
            Assign(ref discordRequiredGuildId, discord["RequiredGuildId"]);
            updatedSettings.DiscordRequiredGuildId = discordRequiredGuildId;

            var discordBotToken = updatedSettings.DiscordBotToken;
            Assign(ref discordBotToken, discord["BotToken"]);
            Assign(ref discordBotToken, configuration["DISCORD_BOT_TOKEN"]);
            updatedSettings.DiscordBotToken = discordBotToken;

            var regolithBase = updatedSettings.RegolithBaseUrl;
            Assign(ref regolithBase, configuration["Regolith:BaseUrl"]);
            updatedSettings.RegolithBaseUrl = regolithBase;

            var regolithKey = updatedSettings.RegolithApiKey;
            Assign(ref regolithKey, configuration["Regolith:ApiKey"]);
            updatedSettings.RegolithApiKey = regolithKey;

            var uexBase = updatedSettings.UexBaseUrl;
            Assign(ref uexBase, configuration["Uex:ApiBaseUrl"]);
            updatedSettings.UexBaseUrl = uexBase;

            var uexKey = updatedSettings.UexCorpApiKey;
            Assign(ref uexKey, configuration["Uex:ApiKey"]);
            updatedSettings.UexCorpApiKey = uexKey;

            var apiBase = updatedSettings.ApiBaseUrl;
            if (apiBase.Contains("example.com", StringComparison.OrdinalIgnoreCase))
                apiBase = string.Empty;
            Assign(ref apiBase, configuration["Api:BaseUrl"]);
            Assign(ref apiBase, DefaultApiBaseUrl);
            updatedSettings.ApiBaseUrl = string.IsNullOrWhiteSpace(apiBase) ? string.Empty : apiBase.TrimEnd('/');

            var blueprintUrl = updatedSettings.BlueprintDataSourceUrl;
            Assign(ref blueprintUrl, configuration["Blueprints:DataSourceUrl"]);
            updatedSettings.BlueprintDataSourceUrl = blueprintUrl;

            updatedSettings = NormalizeSettings(updatedSettings);

            if (!SettingsEqual(_settings, updatedSettings))
                changed = true;

            if (changed)
            {
                _logger.LogInformation("Bootstrapping user settings from configuration defaults.");
                SaveSettingsCore(updatedSettings);
            }
        }
    }

    public void Dispose()
    {
        _watcher.Dispose();
    }

    private void SaveSettingsCore(AppSettings newSettings)
    {
        try
        {
            _logger.LogInformation("Saving user settings to {Path}", _userConfigPath);

            var normalizedSettings = NormalizeSettings(CloneSettings(newSettings));

            var safeCopy = new AppSettings
            {
                PlayerName = normalizedSettings.PlayerName,
                Theme = normalizedSettings.Theme,
                FirstRunComplete = normalizedSettings.FirstRunComplete,
                BlueprintDataSourceUrl = normalizedSettings.BlueprintDataSourceUrl,
                ConnectionString = SecretStorage.Protect(normalizedSettings.ConnectionString),
                JwtKey = SecretStorage.Protect(normalizedSettings.JwtKey),
                JwtIssuer = normalizedSettings.JwtIssuer,
                JwtAudience = normalizedSettings.JwtAudience,
                JwtExpiresInMinutes = normalizedSettings.JwtExpiresInMinutes,
                DiscordClientId = SecretStorage.Protect(normalizedSettings.DiscordClientId),
                DiscordClientSecret = SecretStorage.Protect(normalizedSettings.DiscordClientSecret),
                DiscordCallbackPath = normalizedSettings.DiscordCallbackPath,
                DiscordRequiredGuildId = normalizedSettings.DiscordRequiredGuildId,
                DiscordBotToken = SecretStorage.Protect(normalizedSettings.DiscordBotToken),
                RegolithApiKey = SecretStorage.Protect(normalizedSettings.RegolithApiKey),
                RegolithBaseUrl = normalizedSettings.RegolithBaseUrl,
                UexCorpApiKey = SecretStorage.Protect(normalizedSettings.UexCorpApiKey),
                UexBaseUrl = normalizedSettings.UexBaseUrl,
                ApiBaseUrl = normalizedSettings.ApiBaseUrl,
                GameLogFilePath = SecretStorage.Protect(normalizedSettings.GameLogFilePath),
                DiscordConnected = normalizedSettings.DiscordConnected,
                DiscordAccessToken = SecretStorage.Protect(normalizedSettings.DiscordAccessToken),
                DiscordRefreshToken = SecretStorage.Protect(normalizedSettings.DiscordRefreshToken),
                JwtToken = SecretStorage.Protect(normalizedSettings.JwtToken),
                JwtRefreshToken = SecretStorage.Protect(normalizedSettings.JwtRefreshToken)
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
                IndentedOptions);

            var tempFile = _userConfigPath + ".tmp";
            _suspendWatcherReload = true;
            try
            {
                File.WriteAllText(tempFile, root.ToJsonString(IndentedOptions));
                File.Move(tempFile, _userConfigPath, overwrite: true);
                _settings = normalizedSettings;
            }
            finally
            {
                _suspendWatcherReload = false;
            }

            _logger.LogInformation("User settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving user settings");
            throw;
        }
    }

    private static AppSettings CloneSettings(AppSettings source) =>
        new()
        {
            PlayerName = source.PlayerName,
            Theme = source.Theme,
            FirstRunComplete = source.FirstRunComplete,
            ConnectionString = source.ConnectionString,
            BlueprintDataSourceUrl = source.BlueprintDataSourceUrl,
            JwtKey = source.JwtKey,
            JwtIssuer = source.JwtIssuer,
            JwtAudience = source.JwtAudience,
            JwtExpiresInMinutes = source.JwtExpiresInMinutes,
            DiscordClientId = source.DiscordClientId,
            DiscordClientSecret = source.DiscordClientSecret,
            DiscordCallbackPath = source.DiscordCallbackPath,
            DiscordRequiredGuildId = source.DiscordRequiredGuildId,
            DiscordBotToken = source.DiscordBotToken,
            RegolithApiKey = source.RegolithApiKey,
            RegolithBaseUrl = source.RegolithBaseUrl,
            UexCorpApiKey = source.UexCorpApiKey,
            UexBaseUrl = source.UexBaseUrl,
            ApiBaseUrl = source.ApiBaseUrl,
            GameLogFilePath = source.GameLogFilePath,
            DiscordConnected = source.DiscordConnected,
            DiscordAccessToken = source.DiscordAccessToken,
            DiscordRefreshToken = source.DiscordRefreshToken,
            JwtToken = source.JwtToken,
            JwtRefreshToken = source.JwtRefreshToken
        };

    private const string DefaultApiBaseUrl = "https://packtracker-yke3.onrender.com";

    private static AppSettings NormalizeSettings(AppSettings settings)
    {
        settings.ApiBaseUrl = string.IsNullOrWhiteSpace(settings.ApiBaseUrl)
            ? DefaultApiBaseUrl
            : settings.ApiBaseUrl.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(settings.BlueprintDataSourceUrl)
            || settings.BlueprintDataSourceUrl.Contains("raw.githubusercontent.com"))
        {
            settings.BlueprintDataSourceUrl = DefaultBlueprintUrl;
        }

        if (string.IsNullOrWhiteSpace(settings.DiscordCallbackPath))
            settings.DiscordCallbackPath = "/signin-discord";

        if (string.IsNullOrWhiteSpace(settings.JwtIssuer))
            settings.JwtIssuer = "PackTracker";

        if (string.IsNullOrWhiteSpace(settings.JwtAudience))
            settings.JwtAudience = "PackTrackerClients";

        if (settings.JwtExpiresInMinutes <= 0)
            settings.JwtExpiresInMinutes = 60;

        return settings;
    }

    private static bool SettingsEqual(AppSettings left, AppSettings right) =>
        left.PlayerName == right.PlayerName
        && left.Theme == right.Theme
        && left.FirstRunComplete == right.FirstRunComplete
        && left.ConnectionString == right.ConnectionString
        && left.BlueprintDataSourceUrl == right.BlueprintDataSourceUrl
        && left.JwtKey == right.JwtKey
        && left.JwtIssuer == right.JwtIssuer
        && left.JwtAudience == right.JwtAudience
        && left.JwtExpiresInMinutes == right.JwtExpiresInMinutes
        && left.DiscordClientId == right.DiscordClientId
        && left.DiscordClientSecret == right.DiscordClientSecret
        && left.DiscordCallbackPath == right.DiscordCallbackPath
        && left.DiscordRequiredGuildId == right.DiscordRequiredGuildId
        && left.DiscordBotToken == right.DiscordBotToken
        && left.RegolithApiKey == right.RegolithApiKey
        && left.RegolithBaseUrl == right.RegolithBaseUrl
        && left.UexCorpApiKey == right.UexCorpApiKey
        && left.UexBaseUrl == right.UexBaseUrl
        && left.ApiBaseUrl == right.ApiBaseUrl
        && left.GameLogFilePath == right.GameLogFilePath
        && left.DiscordConnected == right.DiscordConnected
        && left.DiscordAccessToken == right.DiscordAccessToken
        && left.DiscordRefreshToken == right.DiscordRefreshToken
        && left.JwtToken == right.JwtToken
        && left.JwtRefreshToken == right.JwtRefreshToken;
}
