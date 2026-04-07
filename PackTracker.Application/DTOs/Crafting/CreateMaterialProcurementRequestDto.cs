using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOs.Crafting;

public sealed class CreateMaterialProcurementRequestDto
{
    public Guid MaterialId { get; set; }
    public Guid? LinkedCraftingRequestId { get; set; }
    public decimal QuantityRequested { get; set; }
    public int MinimumQuality { get; set; } = 1;
    public MaterialFormPreference PreferredForm { get; set; } = MaterialFormPreference.Any;
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;
    public string? DeliveryLocation { get; set; }
    public int? NumberOfHelpersNeeded { get; set; }
    public string? RewardOffered { get; set; }
    public string? Notes { get; set; }
}
