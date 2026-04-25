using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PackTracker.Application.Interfaces;

namespace PackTracker.Presentation.Services;

public sealed class DiscordEventsService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;

    public DiscordEventsService(HttpClient httpClient, ISettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<IReadOnlyList<DiscordEventItem>> GetUpcomingEventsAsync(CancellationToken ct = default)
    {
        var settings = _settingsService.GetSettings();
        var guildId = settings.DiscordRequiredGuildId;
        var botToken = settings.DiscordBotToken;

        if (string.IsNullOrWhiteSpace(guildId) || string.IsNullOrWhiteSpace(botToken))
            return [];

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://discord.com/api/v10/guilds/{guildId}/scheduled-events?with_user_count=true");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Discord API returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");

        var json = await response.Content.ReadAsStringAsync(ct);
        var raw = JsonSerializer.Deserialize<List<DiscordScheduledEventDto>>(json, JsonOpts)
                  ?? new List<DiscordScheduledEventDto>();

        var result = new List<DiscordEventItem>();
        foreach (var e in raw)
        {
            if (e.Status != 1 && e.Status != 2)
                continue;

            var startsAt = ParseUtc(e.ScheduledStartTime);
            // Skip events that ended more than 5 minutes ago (still SCHEDULED in Discord's eyes sometimes)
            if (e.Status == 1 && startsAt < DateTime.UtcNow.AddMinutes(-5))
                continue;

            result.Add(new DiscordEventItem(
                e.Id ?? string.Empty,
                e.Name ?? "(Unnamed Event)",
                e.Description,
                startsAt,
                e.ScheduledEndTime is null ? null : ParseUtc(e.ScheduledEndTime),
                e.Status,
                e.EntityMetadata?.Location,
                e.UserCount));
        }

        result.Sort((a, b) => a.StartsAt.CompareTo(b.StartsAt));
        return result;
    }

    private static DateTime ParseUtc(string? iso)
        => DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt.ToUniversalTime()
            : DateTime.UtcNow;
}

public record DiscordEventItem(
    string Id,
    string Name,
    string? Description,
    DateTime StartsAt,
    DateTime? EndsAt,
    int Status,
    string? Location,
    int? InterestedCount);

internal sealed class DiscordScheduledEventDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    [JsonPropertyName("scheduled_start_time")]
    public string? ScheduledStartTime { get; set; }
    [JsonPropertyName("scheduled_end_time")]
    public string? ScheduledEndTime { get; set; }
    public int Status { get; set; }
    [JsonPropertyName("entity_type")]
    public int EntityType { get; set; }
    [JsonPropertyName("entity_metadata")]
    public DiscordEventMetadataDto? EntityMetadata { get; set; }
    [JsonPropertyName("user_count")]
    public int? UserCount { get; set; }
}

internal sealed class DiscordEventMetadataDto
{
    public string? Location { get; set; }
}
