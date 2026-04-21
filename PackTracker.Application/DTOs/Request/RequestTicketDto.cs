using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOs.Request;

/// <summary>
/// Represents a general request ticket returned by the legacy/general request system.
/// </summary>
public sealed class RequestTicketDto
{
    #region Identity

    /// <summary>
    /// Gets or sets the unique identifier of the request ticket.
    /// </summary>
    public int Id { get; set; }

    #endregion

    #region Core Request Data

    /// <summary>
    /// Gets or sets the title of the request ticket.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the request ticket.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the request kind.
    /// </summary>
    public RequestKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the request priority.
    /// </summary>
    public RequestPriority Priority { get; set; }

    /// <summary>
    /// Gets or sets the current request status.
    /// </summary>
    public RequestStatus Status { get; set; }

    #endregion

    #region Ownership / Assignment

    /// <summary>
    /// Gets or sets the display name of the user who created the request.
    /// </summary>
    public string CreatedByDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the assigned user, if any.
    /// </summary>
    public string? AssignedToDisplayName { get; set; }

    #endregion

    #region Timing

    /// <summary>
    /// Gets or sets the due date, if any.
    /// </summary>
    public DateTime? DueAt { get; set; }

    /// <summary>
    /// Gets or sets when the request was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the request was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the request was completed, if applicable.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    #endregion

    #region Material / Logistics

    /// <summary>
    /// Gets or sets the material name, if applicable.
    /// </summary>
    public string? MaterialName { get; set; }

    /// <summary>
    /// Gets or sets the quantity needed, if applicable.
    /// </summary>
    public int? QuantityNeeded { get; set; }

    /// <summary>
    /// Gets or sets the meeting/delivery location, if applicable.
    /// </summary>
    public string? MeetingLocation { get; set; }

    /// <summary>
    /// Gets or sets the offered reward, if applicable.
    /// </summary>
    public string? RewardOffered { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of claims allowed.
    /// </summary>
    public int MaxClaims { get; set; }

    #endregion
}