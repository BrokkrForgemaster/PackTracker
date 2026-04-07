using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;

namespace PackTracker.Infrastructure.Services;

public sealed class DiscordNotifier : IDiscordNotifier
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<DiscordNotifier> _logger;

    public DiscordNotifier(HttpClient http, IConfiguration cfg, ILogger<DiscordNotifier> logger)
    {
        _http = http;
        _cfg = cfg;
        _logger = logger;
    }

    public async Task NotifyRequestCreatedAsync(RequestTicket t)
    {
        var webhook = _cfg["Discord:RequestsWebhookUrl"];
        if (string.IsNullOrWhiteSpace(webhook))
        {
            _logger.LogDebug("Discord webhook not configured — skipping NotifyRequestCreated for RequestId={RequestId}.", t.Id);
            return;
        }

        var msg = new
        {
            content = $"📬 **New Request:** {t.Title}\n" +
                      $"**Kind:** {t.Kind}\n" +
                      $"**Priority:** {t.Priority}\n" +
                      $"**By:** {t.CreatedByDisplayName}\n" +
                      $"Use `/assist {t.Id}` to help!"
        };

        try
        {
            var response = await _http.PostAsJsonAsync(webhook, msg);
            if (response.IsSuccessStatusCode)
                _logger.LogInformation("Discord webhook sent. Event=RequestCreated RequestId={RequestId} Title={Title}", t.Id, t.Title);
            else
                _logger.LogWarning("Discord webhook returned non-success. Event=RequestCreated RequestId={RequestId} Status={Status}", t.Id, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discord webhook failed. Event=RequestCreated RequestId={RequestId}", t.Id);
        }
    }

    public async Task NotifyRequestCompletedAsync(RequestTicket t)
    {
        var webhook = _cfg["Discord:RequestsWebhookUrl"];
        if (string.IsNullOrWhiteSpace(webhook))
        {
            _logger.LogDebug("Discord webhook not configured — skipping NotifyRequestCompleted for RequestId={RequestId}.", t.Id);
            return;
        }

        var msg = new
        {
            content = $"✅ **Request #{t.Id} completed by {t.CompletedByUserId}** – {t.Title}"
        };

        try
        {
            var response = await _http.PostAsJsonAsync(webhook, msg);
            if (response.IsSuccessStatusCode)
                _logger.LogInformation("Discord webhook sent. Event=RequestCompleted RequestId={RequestId} CompletedBy={CompletedBy}", t.Id, t.CompletedByUserId);
            else
                _logger.LogWarning("Discord webhook returned non-success. Event=RequestCompleted RequestId={RequestId} Status={Status}", t.Id, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discord webhook failed. Event=RequestCompleted RequestId={RequestId}", t.Id);
        }
    }
}
