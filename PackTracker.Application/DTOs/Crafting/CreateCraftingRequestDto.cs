using System.ComponentModel.DataAnnotations;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOs.Crafting;

public sealed class CreateCraftingRequestDto
{
    [Required]
    public Guid BlueprintId { get; set; }

    [Range(1, 1000)]
    public int QuantityRequested { get; set; } = 1;

    [Required]
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;

    [MaxLength(200)]
    public string? DeliveryLocation { get; set; }

    [MaxLength(200)]
    public string? RewardOffered { get; set; }

    public DateTime? RequiredBy { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
