namespace PackTracker.Application.DTOs.Wiki;

/// <summary>
/// Represents a modifier entry returned by the Star Citizen Wiki blueprint API.
/// </summary>
public class WikiModifierDto
{
    /// <summary>
    /// The property key affected by the modifier.
    /// </summary>
    public string? PropertyKey { get; set; }

    /// <summary>
    /// The modifier range across quality levels.
    /// </summary>
    public WikiModifierRangeDto? ModifierRange { get; set; }
}