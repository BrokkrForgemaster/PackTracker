using System.ComponentModel.DataAnnotations;

namespace PackTracker.Application.DTOs.Crafting;

/// <summary>
/// Represents the payload used to add a comment to a crafting or procurement request.
/// </summary>
public sealed class AddRequestCommentDto
{
    #region Comment Content

    /// <summary>
    /// Gets or sets the comment body.
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    #endregion
}

/// <summary>
/// Represents a comment returned from the API for a crafting or procurement request.
/// </summary>
public sealed class RequestCommentDto
{
    #region Identity

    /// <summary>
    /// Gets or sets the unique identifier of the comment.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the request the comment belongs to.
    /// </summary>
    public Guid RequestId { get; set; }

    #endregion

    #region Author / Content

    /// <summary>
    /// Gets or sets the username of the comment author.
    /// </summary>
    public string AuthorUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the comment body.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    #endregion

    #region Audit

    /// <summary>
    /// Gets or sets when the comment was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    #endregion
}