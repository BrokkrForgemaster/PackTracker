using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOS.Request;


public sealed class RequestCreateDto
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public RequestKind Kind { get; set; }
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;
    public DateTime? DueAt { get; set; }
}