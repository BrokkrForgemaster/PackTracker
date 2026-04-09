using PackTracker.Domain.Entities;

namespace PackTracker.Application.Interfaces;

/// <summary>
/// Defines Discord notification operations for request, crafting,
/// procurement, and comment workflow events.
/// </summary>
public interface IDiscordNotifier
{
    #region General Request Tickets

    /// <summary>
    /// Sends a Discord notification when a general request ticket is created.
    /// </summary>
    /// <param name="ticket">The created request ticket.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyRequestCreatedAsync(RequestTicket ticket);

    /// <summary>
    /// Sends a Discord notification when a general request ticket is completed.
    /// </summary>
    /// <param name="ticket">The completed request ticket.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyRequestCompletedAsync(RequestTicket ticket);

    #endregion

    #region Shared Request Comments

    /// <summary>
    /// Sends a Discord notification when a comment is added to a request workflow item.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="requesterUsername">The username of the original requester.</param>
    /// <param name="authorUsername">The username of the comment author.</param>
    /// <param name="content">The comment content.</param>
    /// <param name="assigneeUsername">The currently assigned username, if any.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyCommentAddedAsync(
        Guid requestId,
        string requesterUsername,
        string authorUsername,
        string content,
        string? assigneeUsername = null);

    #endregion

    #region Procurement

    /// <summary>
    /// Sends a Discord notification when a procurement request status changes.
    /// </summary>
    /// <param name="request">The procurement request.</param>
    /// <param name="oldStatus">The previous status value.</param>
    /// <param name="newStatus">The new status value.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyProcurementStatusChangedAsync(
        MaterialProcurementRequest request,
        string? oldStatus,
        string? newStatus);

    #endregion
}