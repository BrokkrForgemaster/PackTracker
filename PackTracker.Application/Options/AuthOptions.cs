namespace PackTracker.Application.Options;

public class AuthOptions
{
    public const string Section = "Authentication";

    public DiscordOptions Discord { get; set; } = new();
    public JwtOptions Jwt { get; set; } = new();
}

public class DiscordOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = "/signin-discord";
    public string RequiredGuildId { get; set; } = string.Empty;
}

public class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "PackTracker";
    public string Audience { get; set; } = "PackTrackerClient";
    public int ExpiresInMinutes { get; set; } = 60;
}
