using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PackTracker.Infrastructure.Discord;

public interface IDiscordAnnouncementService
{
    Task SendRibbonAwardedAsync(
        string recipientName,
        string ribbonName,
        string citation,
        string? imagePath,
        CancellationToken cancellationToken = default);
}

public sealed class DiscordAnnouncementService : IDiscordAnnouncementService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiscordAnnouncementService> _logger;

    public DiscordAnnouncementService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<DiscordAnnouncementService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendRibbonAwardedAsync(
        string recipientName,
        string ribbonName,
        string citation,
        string? imagePath,
        CancellationToken cancellationToken = default)
    {
        var token = _configuration["Discord:BotToken"];
        var channelId = _configuration["Discord:AnnouncementChannelId"];

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(channelId))
        {
            _logger.LogWarning("Discord announcement skipped. Missing bot token or announcement channel ID.");
            return;
        }

        var url = $"https://discord.com/api/v10/channels/{channelId}/messages";

        var payload = new
        {
            embeds = new[]
            {
                new
                {
                    title = "🎖️ Ribbon Awarded",
                    description =
                        $"**{recipientName}** has been awarded the **{ribbonName}**.",
                    color = 0xC2A23A,
                    fields = new[]
                    {
                        new
                        {
                            name = "Citation",
                            value = string.IsNullOrWhiteSpace(citation)
                                ? "No citation provided."
                                : citation,
                            inline = false
                        }
                    },
                    footer = new
                    {
                        text = "House Wolf PackTracker"
                    },
                    timestamp = DateTimeOffset.UtcNow
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", token);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Failed to send Discord ribbon announcement. Status: {Status}. Body: {Body}",
                response.StatusCode,
                body);
        }
    }
}