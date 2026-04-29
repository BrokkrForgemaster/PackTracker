namespace PackTracker.Domain.Entities.Admin;

public class DiscordIntegrationSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool OperationsEnabled { get; set; }
    public bool MedalAnnouncementsEnabled { get; set; }
    public bool RecruitingPostsEnabled { get; set; }
    public string? OperationsChannelId { get; set; }
    public string? MedalAnnouncementsChannelId { get; set; }
    public string? RecruitingPostsChannelId { get; set; }
    public Guid? UpdatedByProfileId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Profile? UpdatedByProfile { get; set; }
}
