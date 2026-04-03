namespace PackTracker.Application.Options;

public class GuideRequestOptions
{
    public const string SectionName = "GuideRequest";

    public ulong ForumChannelId { get; set; }
    public ulong StaffNotifyChannelId { get; set; }
    public ulong GuideRoleId { get; set; }
}