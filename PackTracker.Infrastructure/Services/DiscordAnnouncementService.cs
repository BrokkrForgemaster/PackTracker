using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;

namespace PackTracker.Infrastructure.Services;

/// <summary>
/// Sends operational notifications to Discord using a configured webhook.
/// </summary>
public sealed class DiscordAnnouncementService : IDiscordAnnouncementService
{
    #region Fields

    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiscordAnnouncementService> _logger;
    private readonly HttpClient _httpClient;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscordAnnouncementService"/> class.
    /// </summary>
    /// <param name="http">The HTTP client used to send webhook requests.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public DiscordAnnouncementService(
        HttpClient http,
        IConfiguration configuration,
        ILogger<DiscordAnnouncementService> logger)
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

    public async Task SendRibbonAwardedAsync(
        string recipientName,
        string ribbonName,
        string citation,
        string? imagePath,
        CancellationToken cancellationToken = default)
    {
        var embed = new
        {
            title = $"🎖️ {ribbonName}",
            description = $"**{recipientName}** has earned a new House Wolf ribbon.",
            color = 0x8B0000,
            fields = new[]
            {
                new
                {
                    name = "Citation",
                    value = string.IsNullOrWhiteSpace(citation)
                        ? "*No citation provided.*"
                        : citation,
                    inline = false
                }
            },
            thumbnail = string.IsNullOrWhiteSpace(imagePath)
                ? null
                : new
                {
                    url = imagePath
                },
            footer = new
            {
                text = "House Wolf • PackTracker • Run with the Pack"
            },
            timestamp = DateTimeOffset.UtcNow
        };

        await SendEmbedAsync(embed, cancellationToken);
    }

    private async Task SendEmbedAsync(
        object embed,
        CancellationToken cancellationToken = default)
    {
        var token = _configuration["Discord:BotToken"];
        var channelId = _configuration["Discord:AnnouncementChannelId"];

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(channelId))
        {
            _logger.LogWarning(
                "Discord announcement skipped. Missing bot token or announcement channel ID.");

            return;
        }

        var payload = new
        {
            embeds = new[]
            {
                embed
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://discord.com/api/v10/channels/{channelId}/messages");

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", token);

        request.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogWarning(
                "Failed to send Discord announcement. Status: {Status}. Body: {Body}",
                response.StatusCode,
                body);
        }
    }

    #endregion
}