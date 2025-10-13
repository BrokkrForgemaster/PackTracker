using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOs.Request;

public class RequestCreateDto
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public RequestKind Kind { get; set; }
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;
    public DateTime? DueAt { get; set; }

    public string SkillObjective { get; set; } = "";
    public string GameBuild { get; set; } = "";
    public string PlayerHandle { get; set; } = "";
    public string TimeZone { get; set; } = "";
    public bool HasMic { get; set; }
    public string PlatformSpecs { get; set; } = "";
    public string Availability { get; set; } = "";
    public string CurrentBaseline { get; set; } = "";
    public string AssetsShips { get; set; } = "";
    public string Urgency { get; set; } = "";
    public string GroupPreference { get; set; } = "";
    public string SuccessCriteria { get; set; } = "";
    public string RecordingPermission { get; set; } = "";
}