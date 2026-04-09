namespace PackTracker.Application.DTOs.Wiki;

/// <summary>
/// Represents the min/max quality modifier range for a blueprint modifier.
/// </summary>
public class WikiModifierRangeDto
{
    /// <summary>
    /// Modifier value at minimum quality.
    /// </summary>
    public double? AtMinQuality { get; set; }

    /// <summary>
    /// Modifier value at maximum quality.
    /// </summary>
    public double? AtMaxQuality { get; set; }
}