using PackTracker.Domain.Enums;

namespace PackTracker.Domain.Entities;

/// <summary>
/// Represents a crafting request submitted by a user for a specific blueprint.
/// Crafting requests may be assigned to a crafter, tracked through status changes,
/// commented on, and linked to downstream procurement workflows.
/// </summary>
public class CraftingRequest
{
    #region Identity

    /// <summary>
    /// Gets or sets the unique identifier for the crafting request.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    #endregion

    #region Foreign Keys

    /// <summary>
    /// Gets or sets the identifier of the blueprint to be crafted.
    /// </summary>
    public Guid BlueprintId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the profile that created the request.
    /// </summary>
    public Guid RequesterProfileId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the profile assigned to complete the crafting work.
    /// </summary>
    public Guid? AssignedCrafterProfileId { get; set; }

    #endregion

    #region Request Details

    /// <summary>
    /// Gets or sets the requested quantity to craft.
    /// </summary>
    public int QuantityRequested { get; set; } = 1;

    /// <summary>
    /// Gets or sets the minimum acceptable quality level for the crafted item.
    /// </summary>
    public int MinimumQuality { get; set; } = 1;

    /// <summary>
    /// Gets or sets the reason the request was refused, if applicable.
    /// </summary>
    public string? RefusalReason { get; set; }

    /// <summary>
    /// Gets or sets the priority of the crafting request.
    /// </summary>
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;

    /// <summary>
    /// Gets or sets the current status of the crafting request.
    /// </summary>
    public RequestStatus Status { get; set; } = RequestStatus.Open;

    /// <summary>
    /// Gets or sets the delivery location for the crafted item.
    /// </summary>
    /// <summary>
    /// Gets or sets the display name of the item to be crafted, captured from wiki data at submission time.
    /// </summary>
    public string? ItemName { get; set; }

    public string? DeliveryLocation { get; set; }

    /// <summary>
    /// Gets or sets any reward offered for fulfilling the crafting request.
    /// </summary>
    public string? RewardOffered { get; set; }

    /// <summary>
    /// Gets or sets the requested completion date/time, if one exists.
    /// </summary>
    public DateTime? RequiredBy { get; set; }

    /// <summary>
    /// Gets or sets any notes or special instructions associated with the request.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets who is responsible for supplying materials for this crafting request.
    /// </summary>
    public MaterialSupplyMode MaterialSupplyMode { get; set; } = MaterialSupplyMode.Negotiable;

    #endregion

    #region Audit Fields

    /// <summary>
    /// Gets or sets when the request was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when the request was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when the request was completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Gets or sets the blueprint associated with the request.
    /// </summary>
    public Blueprint? Blueprint { get; set; }

    /// <summary>
    /// Gets or sets the profile that created the request.
    /// </summary>
    public Profile? RequesterProfile { get; set; }

    /// <summary>
    /// Gets or sets the crafter assigned to fulfill the request.
    /// </summary>
    public Profile? AssignedCrafterProfile { get; set; }

    #endregion
}