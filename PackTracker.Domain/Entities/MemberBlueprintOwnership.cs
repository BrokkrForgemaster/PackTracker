using PackTracker.Domain.Enums;

namespace PackTracker.Domain.Entities;

public class MemberBlueprintOwnership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BlueprintId { get; set; }
    public Guid MemberProfileId { get; set; }
    public BlueprintOwnershipStatus OwnershipStatus { get; set; } = BlueprintOwnershipStatus.Claimed;
    public Guid? VerifiedByProfileId { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string AvailabilityStatus { get; set; } = "Available";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Blueprint? Blueprint { get; set; }
    public Profile? MemberProfile { get; set; }
    public Profile? VerifiedByProfile { get; set; }
}
