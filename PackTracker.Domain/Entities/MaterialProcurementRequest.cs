using PackTracker.Domain.Enums;

namespace PackTracker.Domain.Entities;

public class MaterialProcurementRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? LinkedCraftingRequestId { get; set; }
    public Guid MaterialId { get; set; }
    public decimal QuantityRequested { get; set; }
    public decimal QuantityDelivered { get; set; }
    public MaterialFormPreference PreferredForm { get; set; } = MaterialFormPreference.Any;
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;
    public RequestStatus Status { get; set; } = RequestStatus.Open;
    public string? DeliveryLocation { get; set; }
    public Guid? AssignedToProfileId { get; set; }
    public int? NumberOfHelpersNeeded { get; set; }
    public string? RewardOffered { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public CraftingRequest? LinkedCraftingRequest { get; set; }
    public Material? Material { get; set; }
    public Profile? AssignedToProfile { get; set; }
}
