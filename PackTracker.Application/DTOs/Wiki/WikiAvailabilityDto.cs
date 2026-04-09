namespace PackTracker.Application.DTOs.Wiki;

/// <summary>
/// Represents how a blueprint is acquired/unlocked.
/// </summary>
public class WikiAvailabilityDto
{
    public bool Default { get; set; }

    public List<object> RewardPools { get; set; } = new();
}