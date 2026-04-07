using System.ComponentModel.DataAnnotations;

namespace PackTracker.Application.DTOs.Crafting;

public sealed class RefuseRequestDto
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}
