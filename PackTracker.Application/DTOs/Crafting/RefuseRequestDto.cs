using System.ComponentModel.DataAnnotations;

namespace PackTracker.Application.DTOs.Crafting;

/// <summary>
/// Represents a refusal payload for a crafting or procurement workflow.
/// </summary>
public sealed class RefuseRequestDto
{
    #region Reason

    /// <summary>
    /// Gets or sets the refusal reason.
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    #endregion
}