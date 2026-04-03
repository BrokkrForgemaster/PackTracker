namespace PackTracker.Application.DTOs.Crafting;

public sealed class BlueprintOwnerDto
{
    public Guid MemberProfileId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string InterestType { get; set; } = string.Empty;
    public string OwnershipStatus { get; set; } = string.Empty;
    public string AvailabilityStatus { get; set; } = string.Empty;
    public DateTime? VerifiedAt { get; set; }
    public string? Notes { get; set; }
}
