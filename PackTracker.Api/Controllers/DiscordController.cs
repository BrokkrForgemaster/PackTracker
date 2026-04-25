using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PackTracker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DiscordController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiscordController> _logger;

    public DiscordController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<DiscordController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyList<DiscordEventDto>>> GetEvents(CancellationToken ct)
    {
        var guildId = _configuration["Authentication:Discord:RequiredGuildId"];
        var botToken = _configuration["Authentication:Discord:BotToken"];

        if (string.IsNullOrWhiteSpace(guildId) || string.IsNullOrWhiteSpace(botToken))
        {
            _logger.LogWarning("Discord events requested but BotToken or GuildId is not configured on the server.");
            return Ok(Array.Empty<DiscordEventDto>());
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://discord.com/api/v10/guilds/{guildId}/scheduled-events?with_user_count=true");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Discord API returned {Status}: {Body}", (int)response.StatusCode, body);
                return StatusCode(502, "Failed to fetch events from Discord.");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var raw = JsonSerializer.Deserialize<List<DiscordScheduledEventDto>>(json, JsonOpts) ?? [];

            var now = DateTime.UtcNow;
            var result = new List<DiscordEventDto>();

            foreach (var e in raw)
            {
                if (e.Status != 1 && e.Status != 2)
                    continue;

                var startsAt = ParseUtc(e.ScheduledStartTime);
                if (e.Status == 1 && startsAt < now.AddMinutes(-5))
                    continue;

                result.Add(new DiscordEventDto(
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
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching Discord events.");
            return StatusCode(502, "Failed to fetch events from Discord.");
        }
    }

    private static DateTime ParseUtc(string? iso)
        => DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt.ToUniversalTime()
            : DateTime.UtcNow;
}

public record DiscordEventDto(
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
    [JsonPropertyName("scheduled_start_time")] public string? ScheduledStartTime { get; set; }
    [JsonPropertyName("scheduled_end_time")] public string? ScheduledEndTime { get; set; }
    public int Status { get; set; }
    [JsonPropertyName("entity_metadata")] public DiscordEventMetadataDto? EntityMetadata { get; set; }
    [JsonPropertyName("user_count")] public int? UserCount { get; set; }
}

internal sealed class DiscordEventMetadataDto
{
    public string? Location { get; set; }
}
