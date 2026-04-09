namespace PackTracker.Application.DTOs.Crafting;

/// <summary>
/// Represents a material requirement within a blueprint recipe.
/// </summary>
public sealed class BlueprintRecipeMaterialDto
{
    #region Identity / Classification

    /// <summary>
    /// Gets or sets the material identifier.
    /// </summary>
    public Guid MaterialId { get; set; }

    /// <summary>
    /// Gets or sets the material display name.
    /// </summary>
    public string MaterialName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the material type.
    /// </summary>
    public string MaterialType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the material tier.
    /// </summary>
    public string Tier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source type label for the material.
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    #endregion

    #region Quantity / Usage

    /// <summary>
    /// Gets or sets the quantity required by the recipe.
    /// </summary>
    public double QuantityRequired { get; set; }

    /// <summary>
    /// Gets or sets the unit used by the recipe quantity.
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the material is optional.
    /// </summary>
    public bool IsOptional { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the material can itself be crafted as an intermediate.
    /// </summary>
    public bool IsIntermediateCraftable { get; set; }

    #endregion
}