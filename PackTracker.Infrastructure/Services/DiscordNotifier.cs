using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;

namespace PackTracker.Infrastructure.Services;

public sealed class DiscordNotifier : IDiscordNotifier
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    public DiscordNotifier(HttpClient http, IConfiguration cfg)
    {
        _http = http;
        _cfg = cfg;
    }

    public async Task NotifyRequestCreatedAsync(RequestTicket t)
    {
        var webhook = _cfg["Discord:RequestsWebhookUrl"];
        if (string.IsNullOrWhiteSpace(webhook)) return;

        var msg = new
        {
            content = $"📬 **New Request:** {t.Title}\n" +
                      $"**Kind:** {t.Kind}\n" +
                      $"**Priority:** {t.Priority}\n" +
                      $"**By:** {t.CreatedByDisplayName}\n" +
                      $"Use `/assist {t.Id}` to help!"
        };
        await _http.PostAsJsonAsync(webhook, msg);
    }

    public async Task NotifyRequestCompletedAsync(RequestTicket t)
    {
        var webhook = _cfg["Discord:RequestsWebhookUrl"];
        if (string.IsNullOrWhiteSpace(webhook)) return;

        var msg = new
        {
            content = $"✅ **Request #{t.Id} completed by {t.CompletedByUserId}** – {t.Title}"
        };
        await _http.PostAsJsonAsync(webhook, msg);
    }
}
