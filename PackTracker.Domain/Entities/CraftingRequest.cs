using PackTracker.Domain.Enums;

namespace PackTracker.Domain.Entities;

public class CraftingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BlueprintId { get; set; }
    public Guid RequesterProfileId { get; set; }
    public Guid? AssignedCrafterProfileId { get; set; }
    public int QuantityRequested { get; set; } = 1;
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;
    public RequestStatus Status { get; set; } = RequestStatus.Open;
    public string? DeliveryLocation { get; set; }
    public string? RewardOffered { get; set; }
    public DateTime? RequiredBy { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Blueprint? Blueprint { get; set; }
    public Profile? RequesterProfile { get; set; }
    public Profile? AssignedCrafterProfile { get; set; }
}
