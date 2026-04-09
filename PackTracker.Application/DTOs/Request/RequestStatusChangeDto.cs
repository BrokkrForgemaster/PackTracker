namespace PackTracker.Application.DTOs.Request;

/// <summary>
/// Represents an action-based status change request used by workflow services.
/// </summary>
public class RequestStatusChangeDto
{
    #region Action Information

    /// <summary>
    /// Gets or sets the requested workflow action.
    /// Examples: Claim, Refuse, Complete, Cancel, Start.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional reason associated with the action.
    /// This is commonly used for refusal or cancellation scenarios.
    /// </summary>
    public string? Reason { get; set; }

    #endregion
}