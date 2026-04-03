using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOs.Crafting;

public sealed class RegisterBlueprintOwnershipRequest
{
    public MemberBlueprintInterestType InterestType { get; set; } = MemberBlueprintInterestType.Owns;
    public string AvailabilityStatus { get; set; } = "Available";
    public string? Notes { get; set; }
}
