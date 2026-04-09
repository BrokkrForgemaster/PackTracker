namespace PackTracker.Application.DTOs.Crafting;

/// <summary>
/// Represents a user's ownership or interest relationship to a blueprint.
/// </summary>
public sealed class BlueprintOwnerDto
{
    #region Identity

    /// <summary>
    /// Gets or sets the member profile identifier.
    /// </summary>
    public Guid MemberProfileId { get; set; }

    /// <summary>
    /// Gets or sets the member username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    #endregion

    #region Ownership / Interest State

    /// <summary>
    /// Gets or sets the interest type label for the member.
    /// Example: Owns, Wants, Interested.
    /// </summary>
    public string InterestType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ownership verification/status label.
    /// </summary>
    public string OwnershipStatus { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the member's availability status for using or sharing the blueprint.
    /// </summary>
    public string AvailabilityStatus { get; set; } = string.Empty;

    #endregion

    #region Verification / Notes

    /// <summary>
    /// Gets or sets when the ownership record was verified.
    /// </summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary>
    /// Gets or sets any notes associated with the ownership record.
    /// </summary>
    public string? Notes { get; set; }

    #endregion
}