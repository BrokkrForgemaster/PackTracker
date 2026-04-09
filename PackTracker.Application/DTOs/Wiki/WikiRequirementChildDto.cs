namespace PackTracker.Application.DTOs.Wiki;

/// <summary>
/// Represents a child requirement entry returned by the Star Citizen Wiki blueprint API.
/// Usually this is a resource/material entry.
/// </summary>
public class WikiRequirementChildDto
{
    /// <summary>
    /// The kind of requirement item, such as "resource".
    /// </summary>
    public string? Kind { get; set; }

    /// <summary>
    /// The wiki UUID for the child resource if available.
    /// </summary>
    public string? Uuid { get; set; }

    /// <summary>
    /// Friendly display name of the child resource.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Generic quantity value when SCU is not provided.
    /// </summary>
    public double? Quantity { get; set; }

    /// <summary>
    /// Quantity in SCU when applicable.
    /// </summary>
    public double? QuantityScu { get; set; }
}