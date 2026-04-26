using System.ComponentModel.DataAnnotations;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOs.Crafting;

/// <summary>
/// Represents the payload required to create a new crafting request.
/// </summary>
public sealed class CreateCraftingRequestDto
{
    #region Blueprint / Quantity

    /// <summary>
    /// Gets or sets the blueprint identifier to craft.
    /// </summary>
    [Required]
    public Guid BlueprintId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the crafted item (e.g. "FS-9 LMG").
    /// Used to repair stale placeholder names in the blueprint record.
    /// </summary>
    [MaxLength(300)]
    public string? CraftedItemName { get; set; }

    /// <summary>
    /// Gets or sets the quantity requested.
    /// </summary>
    [Range(1, 1000)]
    public int QuantityRequested { get; set; } = 1;

    /// <summary>
    /// Gets or sets the minimum acceptable quality.
    /// </summary>
    [Range(1, 1000)]
    public int MinimumQuality { get; set; } = 500;

    #endregion

    #region Workflow / Delivery

    /// <summary>
    /// Gets or sets the request priority.
    /// </summary>
    [Required]
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;

    /// <summary>
    /// Gets or sets who is responsible for supplying materials.
    /// </summary>
    public MaterialSupplyMode MaterialSupplyMode { get; set; } = MaterialSupplyMode.Negotiable;

    /// <summary>
    /// Gets or sets the delivery location.
    /// </summary>
    [MaxLength(200)]
    public string? DeliveryLocation { get; set; }

    /// <summary>
    /// Gets or sets the reward offered for fulfilling the request.
    /// </summary>
    [MaxLength(100)]
    public string? RewardOffered { get; set; }

    /// <summary>
    /// Gets or sets the requested completion date.
    /// </summary>
    public DateTime? RequiredBy { get; set; }

    /// <summary>
    /// Gets or sets optional notes associated with the request.
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets the requester's local time zone designation captured by the client.
    /// Example: "Eastern Daylight Time".
    /// </summary>
    [MaxLength(200)]
    public string? RequesterTimeZoneDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the requester's UTC offset in minutes at the time of submission.
    /// </summary>
    public int? RequesterUtcOffsetMinutes { get; set; }

    /// <summary>
    /// Gets or sets whether the request is pinned to the top of the dashboard.
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of members who can claim this request.
    /// If null, default is 1.
    /// </summary>
    [Range(1, 1000)]
    public int? MaxClaims { get; set; }

    #endregion
}
