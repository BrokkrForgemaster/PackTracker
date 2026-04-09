namespace PackTracker.Domain.Entities;

/// <summary>
/// Represents a user profile within the PackTracker system.
/// Profiles are synchronized with Discord identity and enriched
/// with application-specific metadata such as rank and activity.
/// </summary>
public class Profile
{
    #region Identity

    /// <summary>
    /// Internal unique identifier (primary key).
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Discord user ID (snowflake).
    /// </summary>
    public string DiscordId { get; set; } = string.Empty;

    /// <summary>
    /// Username (global or fallback if display name not set).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Discord discriminator (legacy, may be deprecated).
    /// </summary>
    public string Discriminator { get; set; } = string.Empty;

    #endregion

    #region Display / Discord Metadata

    /// <summary>
    /// Display name from Discord (nickname / global display name).
    /// </summary>
    public string? DiscordDisplayName { get; set; }

    /// <summary>
    /// Rank or role within the organization.
    /// </summary>
    public string? DiscordRank { get; set; }

    /// <summary>
    /// Avatar URL from Discord.
    /// </summary>
    public string? DiscordAvatarUrl { get; set; }

    #endregion

    #region Presence / Activity

    /// <summary>
    /// Indicates whether the user is currently online in the application.
    /// (Will be driven by SignalR presence system)
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// Gets or sets the last known activity timestamp.
    /// </summary>
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    #endregion

    #region Audit

    /// <summary>
    /// Profile creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last login timestamp.
    /// </summary>
    public DateTime LastLogin { get; set; } = DateTime.UtcNow;

    #endregion
}