using PackTracker.Domain.Entities;

namespace PackTracker.Application.Interfaces;

/// <summary>
/// Defines Discord notification operations for request, crafting,
/// procurement, awards, and comment workflow events.
/// </summary>
public interface IDiscordAnnouncementService
{
    #region General Request Tickets

    Task NotifyRequestCreatedAsync(RequestTicket ticket);

    Task NotifyRequestCompletedAsync(RequestTicket ticket);

    #endregion

    #region Shared Request Comments

    Task NotifyCommentAddedAsync(
        Guid requestId,
        string requesterUsername,
        string authorUsername,
        string content,
        string? assigneeUsername = null);

    #endregion

    #region Claims

    Task NotifyRequestClaimedAsync(
        string requestType,
        string requestLabel,
        string requesterDisplayName,
        string claimerDisplayName,
        Guid requestId);

    #endregion

    #region Procurement

    Task NotifyProcurementStatusChangedAsync(
        MaterialProcurementRequest request,
        string? oldStatus,
        string? newStatus);

    #endregion

    #region Awards

    /// <summary>
    /// Sends a House Wolf Discord announcement when a ribbon is awarded.
    /// </summary>
    Task SendRibbonAwardedAsync(
        string recipientName,
        string ribbonName,
        string citation,
        string? imagePath,
        CancellationToken cancellationToken = default);

    #endregion
}