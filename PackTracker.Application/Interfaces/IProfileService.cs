using PackTracker.Domain.Entities;

namespace PackTracker.Application.Interfaces;

/// <summary>
/// Defines operations for managing user profiles synchronized from Discord authentication
/// and enriched with application-specific presence and identity metadata.
/// </summary>
public interface IProfileService
{
    #region Upsert / Sync

    /// <summary>
    /// Creates or updates a profile using Discord identity data.
    /// Returns <c>null</c> when the user is not allowed to join the system
    /// (for example, when they are not a member of the required Discord guild).
    /// </summary>
    /// <param name="accessToken">The Discord OAuth access token.</param>
    /// <param name="discordId">The Discord user ID.</param>
    /// <param name="username">The Discord username.</param>
    /// <param name="avatarUrl">The Discord avatar URL, if available.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created or updated profile, or <c>null</c> if access is not allowed.</returns>
    Task<Profile?> UpsertFromDiscordAsync(
        string accessToken,
        string discordId,
        string username,
        string? avatarUrl,
        CancellationToken ct);

    #endregion

    #region Queries

    /// <summary>
    /// Retrieves a profile by its internal identifier.
    /// </summary>
    /// <param name="id">The internal profile identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The matching profile, or <c>null</c> if not found.</returns>
    Task<Profile?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Retrieves a profile by its Discord user identifier.
    /// </summary>
    /// <param name="discordId">The Discord user ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The matching profile, or <c>null</c> if not found.</returns>
    Task<Profile?> GetByDiscordIdAsync(string discordId, CancellationToken ct);

    /// <summary>
    /// Retrieves a profile by username.
    /// </summary>
    /// <param name="name">The username to search for.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The matching profile, or <c>null</c> if not found.</returns>
    Task<Profile?> GetByNameAsync(string name, CancellationToken ct);

    /// <summary>
    /// Retrieves all profiles in the system.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A collection of all profiles.</returns>
    Task<IEnumerable<Profile>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// Retrieves all profiles currently marked as online.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A collection of online profiles.</returns>
    Task<IEnumerable<Profile>> GetOnlineAsync(CancellationToken ct);

    #endregion

    #region Presence

    /// <summary>
    /// Marks a profile as online and updates last-seen metadata.
    /// </summary>
    /// <param name="discordId">The Discord user ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated profile, or <c>null</c> if no matching profile exists.</returns>
    Task<Profile?> MarkOnlineAsync(string discordId, CancellationToken ct);

    /// <summary>
    /// Marks a profile as offline and updates last-seen metadata.
    /// </summary>
    /// <param name="discordId">The Discord user ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated profile, or <c>null</c> if no matching profile exists.</returns>
    Task<Profile?> MarkOfflineAsync(string discordId, CancellationToken ct);

    /// <summary>
    /// Updates the last-seen timestamp for the specified profile.
    /// </summary>
    /// <param name="discordId">The Discord user ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated profile, or <c>null</c> if no matching profile exists.</returns>
    Task<Profile?> TouchLastSeenAsync(string discordId, CancellationToken ct);

    /// <summary>
    /// Updates the self-managed showcase fields for the specified Discord user.
    /// </summary>
    /// <param name="discordId">The Discord user ID.</param>
    /// <param name="showcaseImageUrl">Optional portrait image URL.</param>
    /// <param name="showcaseEyebrow">Optional upper label.</param>
    /// <param name="showcaseTagline">Optional role line.</param>
    /// <param name="showcaseBio">Optional biography text.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated profile, or <c>null</c> if the profile does not exist.</returns>
    Task<Profile?> UpdateShowcaseAsync(
        string discordId,
        string? showcaseImageUrl,
        string? showcaseEyebrow,
        string? showcaseTagline,
        string? showcaseBio,
        CancellationToken ct);

    #endregion
}
