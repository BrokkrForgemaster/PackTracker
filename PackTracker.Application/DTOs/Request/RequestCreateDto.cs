using System.ComponentModel.DataAnnotations;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.DTOs.Request;

/// <summary>
/// Represents the payload used to create a general request ticket.
/// </summary>
public sealed class RequestCreateDto
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

    #endregion

    #region Scheduling

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
    /// Gets or sets the maximum number of members who can claim this request.
    /// If null, default is 1. Use a large number for "infinite".
    /// </summary>
    [Range(1, 1000)]
    public int? MaxClaims { get; set; }

    /// <summary>
    /// Gets or sets whether the request should be pinned to the top of the dashboard.
    /// </summary>
    public bool IsPinned { get; set; }

    #endregion
}