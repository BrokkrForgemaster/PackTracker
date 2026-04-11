namespace PackTracker.Application.DTOs.Crafting;

/// <summary>
/// Represents a lightweight procurement request record returned for list and detail views.
/// </summary>
public sealed class MaterialProcurementRequestListItemDto
{
    #region Identity / Material

    /// <summary>
    /// Gets or sets the procurement request identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the requested material identifier.
    /// </summary>
    public Guid MaterialId { get; set; }

    /// <summary>
    /// Gets or sets the linked crafting request identifier, if any.
    /// </summary>
    public Guid? LinkedCraftingRequestId { get; set; }

    /// <summary>
    /// Gets or sets the material name.
    /// </summary>
    public string MaterialName { get; set; } = string.Empty;

    #endregion

    #region Ownership / Assignment

    /// <summary>
    /// Gets or sets the username of the user who created the procurement request.
    /// </summary>
    public string RequesterUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord display name of the requester (falls back to username).
    /// </summary>
    public string RequesterDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the username of the assigned fulfiller, if any.
    /// </summary>
    public string? AssignedToUsername { get; set; }

    #endregion

    #region Request Details

    /// <summary>
    /// Gets or sets the quantity requested.
    /// </summary>
    public decimal QuantityRequested { get; set; }

    /// <summary>
    /// Gets or sets the quantity delivered so far.
    /// </summary>
    public decimal QuantityDelivered { get; set; }

    /// <summary>
    /// Gets or sets the minimum acceptable quality.
    /// </summary>
    public int MinimumQuality { get; set; }

    /// <summary>
    /// Gets or sets the preferred form as a display string.
    /// </summary>
    public string PreferredForm { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the priority as a display string.
    /// </summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the status as a display string.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the delivery location.
    /// </summary>
    public string? DeliveryLocation { get; set; }

    /// <summary>
    /// Gets or sets the number of helpers required.
    /// </summary>
    public int? NumberOfHelpersNeeded { get; set; }

    /// <summary>
    /// Gets or sets the reward offered.
    /// </summary>
    public string? RewardOffered { get; set; }

    /// <summary>
    /// Gets or sets optional notes associated with the request.
    /// </summary>
    public string? Notes { get; set; }

    #endregion

    #region Audit

    /// <summary>
    /// Gets or sets when the request was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    #endregion
}