using PackTracker.Domain.Enums;

namespace PackTracker.Domain.Entities;

/// <summary>
/// Represents a material procurement request used to gather or transport
/// materials required for crafting or organizational logistics.
/// </summary>
public class MaterialProcurementRequest
{
    #region Identity

    /// <summary>
    /// Gets or sets the unique identifier for the procurement request.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    #endregion

    #region Foreign Keys

    /// <summary>
    /// Gets or sets the identifier of the linked crafting request, if this procurement
    /// request was created to support a crafting workflow.
    /// </summary>
    public Guid? LinkedCraftingRequestId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the material being requested.
    /// </summary>
    public Guid MaterialId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the profile that created the procurement request.
    /// </summary>
    public Guid? RequesterProfileId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the profile assigned to fulfill the request.
    /// </summary>
    public Guid? AssignedToProfileId { get; set; }

    #endregion

    #region Request Details

    /// <summary>
    /// Gets or sets the total quantity requested.
    /// </summary>
    public decimal QuantityRequested { get; set; }

    /// <summary>
    /// Gets or sets the total quantity delivered so far.
    /// </summary>
    public decimal QuantityDelivered { get; set; }

    /// <summary>
    /// Gets or sets the minimum acceptable quality of the requested material.
    /// </summary>
    public int MinimumQuality { get; set; } = 1;

    /// <summary>
    /// Gets or sets the preferred material form.
    /// </summary>
    public MaterialFormPreference PreferredForm { get; set; } = MaterialFormPreference.Any;

    /// <summary>
    /// Gets or sets the priority of the procurement request.
    /// </summary>
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;

    /// <summary>
    /// Gets or sets the current status of the procurement request.
    /// </summary>
    public RequestStatus Status { get; set; } = RequestStatus.Open;

    /// <summary>
    /// Gets or sets the delivery location for the materials.
    /// </summary>
    public string? DeliveryLocation { get; set; }

    /// <summary>
    /// Gets or sets the number of helpers needed to fulfill the request.
    /// </summary>
    public int? NumberOfHelpersNeeded { get; set; }

    /// <summary>
    /// Gets or sets any reward offered for fulfilling the request.
    /// </summary>
    public string? RewardOffered { get; set; }

    /// <summary>
    /// Gets or sets notes or instructions associated with the request.
    /// </summary>
    public string? Notes { get; set; }

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
    /// Gets or sets the linked crafting request, if one exists.
    /// </summary>
    public CraftingRequest? LinkedCraftingRequest { get; set; }

    /// <summary>
    /// Gets or sets the material associated with the request.
    /// </summary>
    public Material? Material { get; set; }

    /// <summary>
    /// Gets or sets the profile that created the procurement request.
    /// </summary>
    public Profile? RequesterProfile { get; set; }

    /// <summary>
    /// Gets or sets the profile assigned to fulfill the procurement request.
    /// </summary>
    public Profile? AssignedToProfile { get; set; }

    #endregion
}