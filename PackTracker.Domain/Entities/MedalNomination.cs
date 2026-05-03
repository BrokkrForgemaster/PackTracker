using PackTracker.Domain.Enums;

namespace PackTracker.Domain.Entities;

public class MedalNomination
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MedalDefinitionId { get; set; }
    public Guid? NomineeProfileId { get; set; }
    public string NomineeName { get; set; } = string.Empty;
    public Guid? NominatorProfileId { get; set; }
    public string NominatorName { get; set; } = string.Empty;
    public string Citation { get; set; } = string.Empty;
    public NominationStatus Status { get; set; } = NominationStatus.Pending;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByProfileId { get; set; }
    public string? ReviewedByName { get; set; }
    public string? ReviewNotes { get; set; }

    public MedalDefinition? MedalDefinition { get; set; }
    public Profile? NomineeProfile { get; set; }
}
