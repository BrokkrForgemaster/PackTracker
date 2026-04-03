namespace PackTracker.Application.DTOs.Profiles;

public class UpsertProfileRequest
{
    public string DiscordId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}
