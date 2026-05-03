namespace PackTracker.Domain.Entities;

public class MedalAward
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MedalDefinitionId { get; set; }
    public Guid? ProfileId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public DateTime? AwardedAt { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public string SourceSystem { get; set; } = "wolf_raid";
    public string? Citation { get; set; }
    public string? AwardedBy { get; set; }

    public MedalDefinition MedalDefinition { get; set; } = null!;
    public Profile? Profile { get; set; }
    public string AwardType { get; set; } = "Medal";
}
