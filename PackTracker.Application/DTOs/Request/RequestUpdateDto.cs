using System.ComponentModel.DataAnnotations;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOs.Request;

/// <summary>
/// Represents the payload used to update a general request ticket.
/// </summary>
public sealed class RequestUpdateDto
{
    #region Core Request Data

    /// <summary>
    /// Gets or sets the title of the request.
    /// </summary>
    [Required]
    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the request.
    /// </summary>
    [MaxLength(4000)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the request kind.
    /// </summary>
    [Required]
    public RequestKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the request priority.
    /// </summary>
    [Required]
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;

    /// <summary>
    /// Gets or sets the current request status.
    /// </summary>
    [Required]
    public RequestStatus Status { get; set; } = RequestStatus.Open;

    #endregion

    #region Assignment / Scheduling

    /// <summary>
    /// Gets or sets the assigned user identifier.
    /// </summary>
    [MaxLength(64)]
    public string? AssignedToUserId { get; set; }

    /// <summary>
    /// Gets or sets the assigned user display name.
    /// </summary>
    [MaxLength(64)]
    public string? AssignedToDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the optional due date/time for the request.
    /// </summary>
    public DateTime? DueAt { get; set; }

    #endregion

    #region Material / Logistics

    /// <summary>
    /// Gets or sets the material name, if this request is material-related.
    /// </summary>
    [MaxLength(100)]
    public string? MaterialName { get; set; }

    /// <summary>
    /// Gets or sets the quantity needed, if applicable.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? QuantityNeeded { get; set; }

    /// <summary>
    /// Gets or sets the meeting or delivery location, if applicable.
    /// </summary>
    [MaxLength(200)]
    public string? MeetingLocation { get; set; }

    /// <summary>
    /// Gets or sets the reward offered, if applicable.
    /// </summary>
    [MaxLength(100)]
    public string? RewardOffered { get; set; }

    /// <summary>
    /// Gets or sets the number of helpers needed, if applicable.
    /// </summary>
    [Range(1, 100)]
    public int? NumberOfHelpersNeeded { get; set; }

    #endregion
}