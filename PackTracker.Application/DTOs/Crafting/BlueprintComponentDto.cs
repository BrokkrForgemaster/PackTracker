namespace PackTracker.Application.DTOs.Crafting;

/// <summary>
/// Represents a single component required to craft a blueprint.
/// </summary>
public sealed class BlueprintComponentDto
{
    /// <summary>
    /// Name of the component/part (e.g. Frame, Barrel).
    /// </summary>
    public string PartName { get; set; } = string.Empty;

    /// <summary>
    /// Material used for this component.
    /// </summary>
    public string MaterialName { get; set; } = string.Empty;

    /// <summary>
    /// Quantity in SCU (or units depending on source).
    /// </summary>
    public double Scu { get; set; }

    /// <summary>
    /// Number of this component required.
    /// </summary>
    public double Quantity { get; set; }

    /// <summary>
    /// Default quality (usually 500 baseline).
    /// </summary>
    public int DefaultQuality { get; set; } = 500;

    /// <summary>
    /// Modifiers applied by this component.
    /// </summary>
    public IReadOnlyList<BlueprintModifierDto> Modifiers { get; set; }
        = Array.Empty<BlueprintModifierDto>();
}