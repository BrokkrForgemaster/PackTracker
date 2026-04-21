using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOs.Request;

/// <summary>
/// Represents a read-model for an assistance request returned by the API.
/// </summary>
public class AssistanceRequestDto
{
    /// <summary>
    /// Gets or sets the unique identifier of the request.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the kind of assistance being requested.
    /// </summary>
    public RequestKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the short title of the request.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the detailed description of the request.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the priority level of the request.
    /// </summary>
    public RequestPriority Priority { get; set; }

    /// <summary>
    /// Gets or sets the current status as a display string.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the request is pinned to the top of the queue.
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Gets or sets the username of the member who created the request.
    /// </summary>
    public string CreatedByUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the member who created the request.
    /// </summary>
    public string CreatedByDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the username of the member assigned to fulfil the request, if any.
    /// </summary>
    public string? AssignedToUsername { get; set; }

    /// <summary>
    /// Gets or sets the material name, if the request is material-related.
    /// </summary>
    public string? MaterialName { get; set; }

    /// <summary>
    /// Gets or sets the quantity needed, if applicable.
    /// </summary>
    public int? QuantityNeeded { get; set; }

    /// <summary>
    /// Gets or sets the meeting or delivery location, if applicable.
    /// </summary>
    public string? MeetingLocation { get; set; }

    /// <summary>
    /// Gets or sets the reward offered by the requester, if any.
    /// </summary>
    public string? RewardOffered { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of members who can claim this request.
    /// </summary>
    public int MaxClaims { get; set; }

    /// <summary>
    /// Gets or sets when the request was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
