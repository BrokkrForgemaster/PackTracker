namespace PackTracker.Application.DTOs.Wiki;

/// <summary>
/// Represents the full blueprint detail response from the Star Citizen Wiki API.
/// </summary>
public class WikiBlueprintDetailDto
{
    #region Identity

    public string Uuid { get; set; } = string.Empty;

    #endregion

    #region Output

    /// <summary>
    /// Full output object describing the crafted item.
    /// </summary>
    public WikiBlueprintOutputDto? Output { get; set; }

    /// <summary>
    /// Fallback name if Output is null.
    /// </summary>
    public string? OutputName { get; set; }

    #endregion

    #region Core Metadata

    public string? GameVersion { get; set; }

    public int CraftTimeSeconds { get; set; }

    #endregion

    #region Availability

    public WikiAvailabilityDto? Availability { get; set; }

    #endregion

    #region Requirements

    public List<WikiRequirementGroupDto> RequirementGroups { get; set; } = new();

    #endregion
}