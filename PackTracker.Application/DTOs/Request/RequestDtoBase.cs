namespace PackTracker.Application.DTOs.Request;

/// <summary>
/// Represents the shared read-model fields returned for request workflows.
/// This base DTO is intended for API responses and UI display models,
/// not for create/update command payloads.
/// </summary>
public abstract class RequestDtoBase
{
    #region Identity

    /// <summary>
    /// Gets or sets the unique identifier of the request.
    /// </summary>
    public Guid Id { get; set; }

    #endregion

    #region Status / Priority

    /// <summary>
    /// Gets or sets the current status of the request.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current priority of the request.
    /// </summary>
    public string Priority { get; set; } = string.Empty;

    #endregion

    #region Ownership / Assignment

    /// <summary>
    /// Gets or sets the username of the user who created the request.
    /// </summary>
    public string RequesterUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the username of the currently assigned user, if any.
    /// </summary>
    public string? AssignedToUsername { get; set; }

    #endregion

    #region Additional Metadata

    /// <summary>
    /// Gets or sets any notes associated with the request.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets when the request was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the request was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    public string? AssignedCrafterUsername { get; set; }

    #endregion
}