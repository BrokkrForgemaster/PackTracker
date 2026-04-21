namespace PackTracker.Domain.Entities;

/// <summary>
/// Represents a claim on a request by a member.
/// Multiple members can claim a single request if the request allows it.
/// </summary>
public class RequestClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The type of request being claimed.
    /// "Assistance", "Crafting", "Procurement"
    /// </summary>
    public string RequestType { get; set; } = string.Empty;

    /// <summary>
    /// The unique identifier of the request.
    /// </summary>
    public Guid RequestId { get; set; }

    /// <summary>
    /// The profile identifier of the member who claimed the request.
    /// </summary>
    public Guid ProfileId { get; set; }

    /// <summary>
    /// When the claim was made.
    /// </summary>
    public DateTime ClaimedAt { get; set; } = DateTime.UtcNow;

    #region Navigation Properties

    public Profile? Profile { get; set; }

    #endregion
}
