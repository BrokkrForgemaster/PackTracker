namespace PackTracker.Application.DTOs.Request;

/// <summary>
/// Represents a lightweight payload for changing the status of a request.
/// </summary>
public class UpdateRequestStatusDto
{
    #region Properties

    /// <summary>
    /// Gets or sets the new request status value.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    #endregion
}