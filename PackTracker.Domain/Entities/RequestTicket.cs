using PackTracker.Domain.Enums;

namespace PackTracker.Domain.Entities;

/// <summary>
/// Represents a general request ticket submitted by a user.
/// This is the shared request model used by the Requests Hub workflow.
/// </summary>
public class RequestTicket
{
    #region Core Identity

    /// <summary>
    /// Gets or sets the unique ticket identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the title of the request.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the detailed request description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the request kind.
    /// </summary>
    public RequestKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the request priority.
    /// </summary>
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;

    /// <summary>
    /// Gets or sets the current request status.
    /// </summary>
    public RequestStatus Status { get; set; } = RequestStatus.Open;

    #endregion

    #region Request Ownership

    /// <summary>
    /// Gets or sets the creator's user identifier.
    /// </summary>
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the creator's display name.
    /// </summary>
    public string CreatedByDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assigned user's identifier.
    /// </summary>
    public string? AssignedToUserId { get; set; }

    /// <summary>
    /// Gets or sets the assigned user's display name.
    /// </summary>
    public string? AssignedToDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the completed-by user identifier.
    /// </summary>
    public string? CompletedByUserId { get; set; }

    #endregion

    #region Scheduling

    /// <summary>
    /// Gets or sets the due date and time for the request.
    /// </summary>
    public DateTime? DueAt { get; set; }

    #endregion

    #region Training / Guide Fields

    /// <summary>
    /// Gets or sets the objective or skill target of the request.
    /// </summary>
    public string SkillObjective { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Star Citizen game build associated with the request.
    /// </summary>
    public string GameBuild { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requestor's in-game player handle.
    /// </summary>
    public string PlayerHandle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requestor's time zone.
    /// </summary>
    public string TimeZone { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the requestor has a microphone.
    /// </summary>
    public bool HasMic { get; set; }

    /// <summary>
    /// Gets or sets the requestor's platform specifications.
    /// </summary>
    public string PlatformSpecs { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requestor's availability details.
    /// </summary>
    public string Availability { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requestor's baseline skill or experience level.
    /// </summary>
    public string CurrentBaseline { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requestor's relevant assets or ships.
    /// </summary>
    public string AssetsShips { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the urgency notes for the request.
    /// </summary>
    public string Urgency { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requestor's group preference.
    /// </summary>
    public string GroupPreference { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request success criteria.
    /// </summary>
    public string SuccessCriteria { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recording permission preference.
    /// </summary>
    public string RecordingPermission { get; set; } = string.Empty;

    #endregion

    #region Material / Resource Request Fields

    /// <summary>
    /// Gets or sets the requested material or resource name.
    /// </summary>
    public string? MaterialName { get; set; }

    /// <summary>
    /// Gets or sets the requested quantity.
    /// </summary>
    public int? QuantityNeeded { get; set; }

    /// <summary>
    /// Gets or sets the requested meeting or delivery location.
    /// </summary>
    public string? MeetingLocation { get; set; }

    /// <summary>
    /// Gets or sets the reward being offered.
    /// </summary>
    public string? RewardOffered { get; set; }

    /// <summary>
    /// Gets or sets the number of helpers needed.
    /// </summary>
    public int? NumberOfHelpersNeeded { get; set; }

    #endregion

    #region Auditing

    /// <summary>
    /// Gets or sets the creation timestamp in UTC.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the last update timestamp in UTC.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the completion timestamp in UTC.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    #endregion
}