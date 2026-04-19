using PackTracker.Application.DTOs.Request;

namespace PackTracker.Application.DTOs.Dashboard;

public class DashboardSummaryDto
{
    public List<ActiveRequestDto> ActiveRequests { get; set; } = new();
    public List<GuideRequestDto> ScheduledGuides { get; set; } = new();
}

public class ActiveRequestDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty; // "Assistance", "Crafting", "Procurement"
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public string RequesterDisplayName { get; set; } = string.Empty;
    public string? AssigneeDisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GuideRequestDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ScheduledAt { get; set; }
}
