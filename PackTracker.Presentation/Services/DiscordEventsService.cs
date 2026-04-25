using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PackTracker.Presentation.Services;

public sealed class DiscordEventsService
{
    private readonly IApiClientProvider _apiClientProvider;

    public DiscordEventsService(IApiClientProvider apiClientProvider)
    {
        _apiClientProvider = apiClientProvider;
    }

    public async Task<IReadOnlyList<DiscordEventItem>> GetUpcomingEventsAsync(CancellationToken ct = default)
    {
        using var client = _apiClientProvider.CreateClient();
        using var response = await client.GetAsync("api/v1/discord/events", ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Server returned {(int)response.StatusCode} fetching Discord events.");

        var items = await response.Content.ReadFromJsonAsync<List<DiscordEventItem>>(cancellationToken: ct);
        return items ?? [];
    }
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
