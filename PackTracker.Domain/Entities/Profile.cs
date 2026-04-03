namespace PackTracker.Domain.Entities;

/// <summary name="Profile">
/// Represents a user profile in the PackTracker system.
/// </summary>
public class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();   // internal PK
    public string DiscordId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    
    public string Discriminator { get; set; } = string.Empty; 
    public string AvatarUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLogin { get; set; } = DateTime.UtcNow;
}