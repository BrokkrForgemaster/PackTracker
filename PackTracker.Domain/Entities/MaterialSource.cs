using PackTracker.Domain.Enums;

namespace PackTracker.Domain.Entities;

public class MaterialSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaterialId { get; set; }
    public MaterialSourceType SourceMethod { get; set; } = MaterialSourceType.Unknown;
    public string? Location { get; set; }
    public string? Notes { get; set; }
    public string? SourceVersion { get; set; }
    public string Confidence { get; set; } = "Imported";

    public Material? Material { get; set; }
}
