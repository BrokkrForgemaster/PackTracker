using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;

namespace PackTracker.Infrastructure.Services;

/// <summary>
/// Sends operational notifications to Discord using a configured webhook.
/// </summary>
public sealed class DiscordNotifier : IDiscordNotifier
{
    #region Fields

    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiscordNotifier> _logger;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscordNotifier"/> class.
    /// </summary>
    /// <param name="http">The HTTP client used to send webhook requests.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public DiscordNotifier(
        HttpClient http,
        IConfiguration configuration,
        ILogger<DiscordNotifier> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the configured Discord webhook URL for request notifications.
    /// </summary>
    private string? WebhookUrl => _configuration["Discord:RequestsWebhookUrl"];

    #endregion

    #region General Request Tickets

    /// <inheritdoc />
    public async Task NotifyRequestCreatedAsync(RequestTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var payload = new
        {
            content =
                $"📬 **New Request:** {ticket.Title}\n" +
                $"**Kind:** {ticket.Kind}\n" +
                $"**Priority:** {ticket.Priority}\n" +
                $"**By:** {ticket.CreatedByDisplayName}\n" +
                $"Use `/assist {ticket.Id}` to help!"
        };

        await SendWebhookAsync(payload, "RequestCreated", ticket.Id);
    }

    /// <inheritdoc />
    public async Task NotifyRequestCompletedAsync(RequestTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var completedBy = string.IsNullOrWhiteSpace(ticket.CompletedByUserId)
            ? "Unknown"
            : ticket.CompletedByUserId;

        var payload = new
        {
            content =
                $"✅ **Request #{ticket.Id} completed by {completedBy}** – {ticket.Title}"
        };

        await SendWebhookAsync(payload, "RequestCompleted", ticket.Id);
    }

    #endregion

    #region Shared Request Comments

    /// <inheritdoc />
    public async Task NotifyCommentAddedAsync(
        Guid requestId,
        string requesterUsername,
        string authorUsername,
        string content,
        string? assigneeUsername = null)
    {
        var requester = string.IsNullOrWhiteSpace(requesterUsername) ? "Unknown" : requesterUsername;
        var author = string.IsNullOrWhiteSpace(authorUsername) ? "Unknown" : authorUsername;

        var pingText = string.Empty;

        if (!string.Equals(author, requester, StringComparison.OrdinalIgnoreCase))
        {
            pingText = $"Paging {requester}. ";
        }
        else if (!string.IsNullOrWhiteSpace(assigneeUsername) &&
                 !string.Equals(author, assigneeUsername, StringComparison.OrdinalIgnoreCase))
        {
            pingText = $"Paging {assigneeUsername}. ";
        }

        var payload = new
        {
            content =
                $"💬 **New Comment on Request:** {requestId}\n" +
                $"**From:** {author}\n" +
                $"{pingText}\n" +
                $"> {content}"
        };

        await SendWebhookAsync(payload, "CommentAdded", requestId);
    }

    #endregion

    #region Claims

    /// <inheritdoc />
    public async Task NotifyRequestClaimedAsync(
        string requestType,
        string requestLabel,
        string requesterDisplayName,
        string claimerDisplayName,
        Guid requestId)
    {
        var payload = new
        {
            content =
                $"🤝 **Request Accepted:** {requestLabel}\n" +
                $"**Type:** {requestType}\n" +
                $"**Requested by:** {requesterDisplayName}\n" +
                $"**Accepted by:** {claimerDisplayName}\n" +
                $"**Request ID:** {requestId}"
        };

        await SendWebhookAsync(payload, "RequestClaimed", requestId);
    }

    #endregion

    #region Procurement

    /// <inheritdoc />
    public async Task NotifyProcurementStatusChangedAsync(
        MaterialProcurementRequest request,
        string? oldStatus,
        string? newStatus)
    {
        ArgumentNullException.ThrowIfNull(request);

        var materialName = !string.IsNullOrWhiteSpace(request.Material?.Name)
            ? request.Material.Name
            : "Unknown Material";

        var assignedTo = !string.IsNullOrWhiteSpace(request.AssignedToProfile?.Username)
            ? request.AssignedToProfile.Username
            : "Unassigned";

        var requester = !string.IsNullOrWhiteSpace(request.RequesterProfile?.Username)
            ? request.RequesterProfile.Username
            : "Unknown";

        var payload = new
        {
            content =
                $"🔄 **Procurement Status Change:** {materialName}\n" +
                $"**Request:** {request.Id}\n" +
                $"**Requested By:** {requester}\n" +
                $"**Status:** {oldStatus ?? "Unknown"} ➡️ **{newStatus ?? "Unknown"}**\n" +
                $"**Assigned To:** {assignedTo}"
        };

        await SendWebhookAsync(payload, "ProcurementStatusChanged", request.Id);
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Sends a payload to the configured Discord webhook.
    /// </summary>
    /// <param name="payload">The webhook payload object.</param>
    /// <param name="eventName">The logical event name for logging.</param>
    /// <param name="requestId">The related request identifier.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SendWebhookAsync(object payload, string eventName, object requestId)
    {
        var webhook = WebhookUrl;

        if (string.IsNullOrWhiteSpace(webhook))
        {
            _logger.LogDebug(
                "Discord webhook not configured. Skipping Event={EventName} RequestId={RequestId}",
                eventName,
                requestId);

            return;
        }

        try
        {
            using var response = await _http.PostAsJsonAsync(webhook, payload);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Discord webhook sent successfully. Event={EventName} RequestId={RequestId}",
                    eventName,
                    requestId);
            }
            else
            {
                _logger.LogWarning(
                    "Discord webhook returned non-success. Event={EventName} RequestId={RequestId} StatusCode={StatusCode}",
                    eventName,
                    requestId,
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Discord webhook failed. Event={EventName} RequestId={RequestId}",
                eventName,
                requestId);
        }
    }

    #endregion
}