namespace PackTracker.Domain.Entities;

/// <summary>
/// Represents a comment attached to a request workflow item.
/// A comment may be associated with either a crafting request or
/// a material procurement request by shared request identifier.
/// </summary>
public class RequestComment
{
    #region Identity

    /// <summary>
    /// Gets or sets the unique identifier for the comment.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    #endregion

    #region Foreign Keys

    /// <summary>
    /// Gets or sets the identifier of the request this comment belongs to.
    /// This identifier may correspond to either a crafting request or a material procurement request.
    /// </summary>
    public Guid RequestId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the profile that authored the comment.
    /// </summary>
    public Guid AuthorProfileId { get; set; }

    #endregion

    #region Comment Content

    /// <summary>
    /// Gets or sets the comment content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the comment was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Gets or sets the author profile for the comment.
    /// </summary>
    public Profile? AuthorProfile { get; set; }

    #endregion
}