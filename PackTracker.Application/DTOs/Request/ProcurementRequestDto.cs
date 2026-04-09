namespace PackTracker.Application.DTOs.Request;

/// <summary>
/// Represents a procurement request returned to the client.
/// </summary>
public class ProcurementRequestDto : RequestDtoBase
{
    #region Material Information

    /// <summary>
    /// Gets or sets the identifier of the requested material.
    /// </summary>
    public Guid MaterialId { get; set; }

    /// <summary>
    /// Gets or sets the human-readable name of the requested material.
    /// </summary>
    public string MaterialName { get; set; } = string.Empty;

    #endregion

    #region Request Details

    /// <summary>
    /// Gets or sets the requested quantity of material.
    /// </summary>
    public decimal QuantityRequested { get; set; }

    /// <summary>
    /// Gets or sets the quantity of material delivered so far.
    /// </summary>
    public decimal QuantityDelivered { get; set; }

    /// <summary>
    /// Gets or sets the minimum acceptable quality level.
    /// </summary>
    public int MinimumQuality { get; set; }

    #endregion
}