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
    /// Rank or role within the organization hierarchy.
    /// </summary>
    public string? DiscordRank { get; set; }

    /// <summary>
    /// Division membership (LOCOPS, TACOPS, SPECOPS, ARCOPS, Leadership), if any.
    /// Stored separately from rank as users can belong to a division at any hierarchy rank.
    /// </summary>
    public string? DiscordDivision { get; set; }

    /// <summary>
    /// Avatar URL from Discord.
    /// </summary>
    public string? DiscordAvatarUrl { get; set; }

    /// <summary>
    /// Optional custom portrait image for showcase/leadership cards.
    /// </summary>
    public string? ShowcaseImageUrl { get; set; }

    /// <summary>
    /// Small upper label for showcase cards.
    /// </summary>
    public string? ShowcaseEyebrow { get; set; }

    /// <summary>
    /// Short subtitle or role line for showcase cards.
    /// </summary>
    public string? ShowcaseTagline { get; set; }

    /// <summary>
    /// Long-form biography/description for showcase cards.
    /// </summary>
    public string? ShowcaseBio { get; set; }

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

    /// <summary>
    /// Per-request claim counts this user has already acknowledged in the dashboard.
    /// </summary>
    public Dictionary<string, int> AcknowledgedClaimCounts { get; set; } = new();

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
