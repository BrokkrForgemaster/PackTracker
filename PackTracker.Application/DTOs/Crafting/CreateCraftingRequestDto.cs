using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOs.Crafting;

public sealed class CreateCraftingRequestDto
{
    public Guid BlueprintId { get; set; }
    public int QuantityRequested { get; set; } = 1;
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;
    public string? DeliveryLocation { get; set; }
    public string? RewardOffered { get; set; }
    public DateTime? RequiredBy { get; set; }
    public string? Notes { get; set; }
}
