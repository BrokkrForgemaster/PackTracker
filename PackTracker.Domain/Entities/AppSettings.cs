namespace PackTracker.Domain.Entities;

/// <summary>
/// Represents user-scoped and application-scoped settings.
/// Sensitive values are encrypted at rest via <see cref="SecretStorage"/>.
/// </summary>
public class AppSettings
{
    // --- General ---
    public string PlayerName { get; set; } = string.Empty;
    public string Theme { get; set; } = "Dark";
    public bool FirstRunComplete { get; set; } = false;

    // --- Database ---
    /// <summary>
    /// Encrypted connection string to PostgreSQL.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    // --- JWT Authentication ---
    public string JwtKey { get; set; } = string.Empty;
    public string JwtIssuer { get; set; } = "PackTracker";
    public string JwtAudience { get; set; } = "PackTrackerClients";
    public int JwtExpiresInMinutes { get; set; } = 60;

    // --- Discord Authentication ---
    public string DiscordClientId { get; set; } = string.Empty;
    public string DiscordClientSecret { get; set; } = string.Empty;
    public string DiscordCallbackPath { get; set; } = "/signin-discord";
    public string DiscordRequiredGuildId { get; set; } = string.Empty;

    // --- Regolith API ---
    public string RegolithApiKey { get; set; } = string.Empty;
    public string RegolithBaseUrl { get; set; } = "https://api.regolith.rocks";

    // --- UEX API ---
    public string UexCorpApiKey { get; set; } = string.Empty;
    public string UexBaseUrl { get; set; } = "https://api.uexcorp.uk/2.0";

    // --- Embedded API ---
    public string ApiBaseUrl { get; set; } = "http://localhost:5001";

    // --- Game Integration ---
    /// <summary>
    /// Path to the Star Citizen log folder.
    /// </summary>
    public string GameLogFilePath { get; set; } = string.Empty;

    // --- Misc Integration Flags ---
    /// <summary>
    /// Tracks whether the Discord account is linked to the app.
    /// </summary>
    public string DiscordConnected { get; set; } = string.Empty;
    public string? DiscordAccessToken { get; set; }
    public string? DiscordRefreshToken { get; set; }
    public string? JwtToken { get; set; }
    public string? JwtRefreshToken { get; set; }
    
}
