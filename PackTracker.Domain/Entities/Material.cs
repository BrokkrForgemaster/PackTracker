using PackTracker.Domain.Enums;

namespace PackTracker.Domain.Entities;

public class Material
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public MaterialSourceType SourceType { get; set; } = MaterialSourceType.Unknown;
    public bool IsRawOre { get; set; }
    public bool IsRefinedMaterial { get; set; }
    public bool IsCraftedComponent { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
