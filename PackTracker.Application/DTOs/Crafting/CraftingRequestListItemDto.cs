namespace PackTracker.Application.DTOs.Crafting;

/// <summary>
/// Represents a lightweight crafting request record returned for list and detail views.
/// </summary>
public sealed class CraftingRequestListItemDto
{
    #region Identity / Blueprint

    /// <summary>
    /// Gets or sets the unique identifier of the crafting request.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the blueprint being crafted.
    /// </summary>
    public Guid BlueprintId { get; set; }

    /// <summary>
    /// Gets or sets the blueprint name.
    /// </summary>
    public string BlueprintName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the crafted item name.
    /// </summary>
    public string CraftedItemName { get; set; } = string.Empty;

    #endregion

    #region Ownership / Assignment

    /// <summary>
    /// Gets or sets the username of the user who created the request.
    /// </summary>
    public string RequesterUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord display name of the requester (falls back to username).
    /// </summary>
    public string RequesterDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the username of the assigned crafter, if any.
    /// </summary>
    public string? AssignedCrafterUsername { get; set; }

    #endregion

    #region Request Details

    /// <summary>
    /// Gets or sets the quantity requested.
    /// </summary>
    public int QuantityRequested { get; set; }

    /// <summary>
    /// Gets or sets the minimum quality requested.
    /// </summary>
    public int MinimumQuality { get; set; }

    /// <summary>
    /// Gets or sets the refusal reason, if the request was refused.
    /// </summary>
    public string? RefusalReason { get; set; }

    /// <summary>
    /// Gets or sets the priority value as a display string.
    /// </summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current status value as a display string.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the material supply mode as a display string.
    /// </summary>
    public string MaterialSupplyMode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the delivery location, if specified.
    /// </summary>
    public string? DeliveryLocation { get; set; }

    /// <summary>
    /// Gets or sets the offered reward, if specified.
    /// </summary>
    public string? RewardOffered { get; set; }

    /// <summary>
    /// Gets or sets the requested completion date, if specified.
    /// </summary>
    public DateTime? RequiredBy { get; set; }

    /// <summary>
    /// Gets or sets additional notes for the request.
    /// </summary>
    public string? Notes { get; set; }

    #endregion

    #region Audit / Recipe

    /// <summary>
    /// Gets or sets when the request was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the recipe materials associated with the blueprint.
    /// </summary>
    public IReadOnlyList<BlueprintRecipeMaterialDto> Materials { get; set; } = Array.Empty<BlueprintRecipeMaterialDto>();

    #endregion
}