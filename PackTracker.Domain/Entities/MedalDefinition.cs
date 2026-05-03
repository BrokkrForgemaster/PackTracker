namespace PackTracker.Domain.Entities;

public class MedalDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string SourceSystem { get; set; } = "wolf_raid";
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MedalAward> Awards { get; set; } = new List<MedalAward>();
    public string AwardType { get; set; }
}
