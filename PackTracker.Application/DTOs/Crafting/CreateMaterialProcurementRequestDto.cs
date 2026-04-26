using System.ComponentModel.DataAnnotations;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOs.Crafting;

/// <summary>
/// Represents the payload required to create a material procurement request.
/// </summary>
public sealed class CreateMaterialProcurementRequestDto
{
    #region Material / Linkage

    /// <summary>
    /// Gets or sets the material identifier being requested.
    /// </summary>
    [Required]
    public Guid MaterialId { get; set; }

    /// <summary>
    /// Gets or sets the material name, used as a fallback lookup when MaterialId has no DB match.
    /// </summary>
    [MaxLength(300)]
    public string? MaterialName { get; set; }

    /// <summary>
    /// Gets or sets the linked crafting request identifier, if this request supports crafting.
    /// </summary>
    public Guid? LinkedCraftingRequestId { get; set; }

    #endregion

    #region Request Details

    /// <summary>
    /// Gets or sets the quantity requested.
    /// </summary>
    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal QuantityRequested { get; set; }

    /// <summary>
    /// Gets or sets the minimum acceptable material quality.
    /// </summary>
    [Range(1, 1000)]
    public int MinimumQuality { get; set; } = 1;

    /// <summary>
    /// Gets or sets the preferred material form.
    /// </summary>
    public MaterialFormPreference PreferredForm { get; set; } = MaterialFormPreference.Any;

    /// <summary>
    /// Gets or sets the request priority.
    /// </summary>
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;

    /// <summary>
    /// Gets or sets the desired delivery location.
    /// </summary>
    [MaxLength(200)]
    public string? DeliveryLocation { get; set; }

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

    /// <summary>
    /// Gets or sets the reward offered for fulfilling the request.
    /// </summary>
    [MaxLength(100)]
    public string? RewardOffered { get; set; }

    /// <summary>
    /// Gets or sets optional notes associated with the procurement request.
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    #endregion
}
