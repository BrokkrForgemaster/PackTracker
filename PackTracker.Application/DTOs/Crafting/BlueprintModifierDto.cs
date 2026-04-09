namespace PackTracker.Application.DTOs.Crafting;

/// <summary>
/// Represents a stat modifier for a component.
/// </summary>
public sealed class BlueprintModifierDto
{
    /// <summary>
    /// Stat key (e.g. weapon_recoil_kick).
    /// </summary>
    public string PropertyKey { get; set; } = string.Empty;

    /// <summary>
    /// Value at minimum quality (0).
    /// </summary>
    public double AtMinQuality { get; set; }

    /// <summary>
    /// Value at maximum quality (1000).
    /// </summary>
    public double AtMaxQuality { get; set; }

    /// <summary>
    /// Optional precomputed value at 1000 (if provided by API).
    /// </summary>
    public double? ValueAt1000 { get; set; }
}