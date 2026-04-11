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
    public string ConnectionString { get; set; } = string.Empty;

    // --- Blueprint / Crafting Data Source ---
    // Star Citizen Wiki API — free, no key required, has ingredients per blueprint
    public string BlueprintDataSourceUrl { get; set; } = "https://api.star-citizen.wiki/api/blueprints";

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

    // --- UEX API (trading/economy data — no blueprint support) ---
    public string UexCorpApiKey { get; set; } = string.Empty;
    public string UexBaseUrl { get; set; } = "https://api.uexcorp.space/2.0";

    // --- External API ---
    public string ApiBaseUrl { get; set; } = string.Empty;

    // --- Game Integration ---
    public string GameLogFilePath { get; set; } = string.Empty;

    // --- Misc Integration Flags ---
    public string DiscordConnected { get; set; } = string.Empty;
    public string? DiscordAccessToken { get; set; }
    public string? DiscordRefreshToken { get; set; }
    public string? JwtToken { get; set; }
    public string? JwtRefreshToken { get; set; }
}
