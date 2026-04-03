namespace PackTracker.Application.DTOs.Crafting;

public sealed class RegisterBlueprintOwnershipRequest
{
    public string AvailabilityStatus { get; set; } = "Available";
    public string? Notes { get; set; }
}
