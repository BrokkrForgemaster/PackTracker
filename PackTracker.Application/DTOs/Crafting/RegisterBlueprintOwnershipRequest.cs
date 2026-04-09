using System.ComponentModel.DataAnnotations;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOs.Crafting;

/// <summary>
/// Represents the payload used to register blueprint ownership or interest for a member.
/// </summary>
public sealed class RegisterBlueprintOwnershipRequest
{
    #region Ownership / Interest

    /// <summary>
    /// Gets or sets the member's interest type for the blueprint.
    /// </summary>
    public MemberBlueprintInterestType InterestType { get; set; } = MemberBlueprintInterestType.Owns;

    /// <summary>
    /// Gets or sets the current availability status.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string AvailabilityStatus { get; set; } = "Available";

    /// <summary>
    /// Gets or sets optional notes associated with the ownership record.
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    #endregion
}