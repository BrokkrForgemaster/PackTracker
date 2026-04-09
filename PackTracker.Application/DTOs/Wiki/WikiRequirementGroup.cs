namespace PackTracker.Application.DTOs.Wiki;

/// <summary>
/// Represents a requirement group returned by the Star Citizen Wiki blueprint API.
/// A group typically contains one or more resource/material children plus modifier data.
/// </summary>
public class WikiRequirementGroupDto
{
    /// <summary>
    /// Friendly display name of the requirement group.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Raw key/identifier for the requirement group.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Child requirement items in the group.
    /// </summary>
    public List<WikiRequirementChildDto> Children { get; set; } = new();

    /// <summary>
    /// Modifiers associated with this requirement group.
    /// </summary>
    public List<WikiModifierDto> Modifiers { get; set; } = new();

    /// <summary>
    /// Number of this component required.
    /// </summary>
    public int? RequiredCount { get; set; }
}