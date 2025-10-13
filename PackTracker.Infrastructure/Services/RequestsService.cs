using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Request;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Infrastructure.Services;

public class RequestsService : IRequestsService
{
    private readonly HttpClient _http;
    private readonly ILogger<RequestsService> _logger;

    public RequestsService(IHttpClientFactory f, ILogger<RequestsService> logger)
    {
        _http = f.CreateClient("default"); // you already configure base + auth
        _logger = logger;
    }

    public async Task<List<RequestDto>> QueryAsync(RequestStatus? status = null, RequestKind? kind = null, bool? mine = null, int top = 100, CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (status.HasValue) qs.Add($"status={(int)status.Value}");
        if (kind.HasValue) qs.Add($"kind={(int)kind.Value}");
        if (mine.HasValue) qs.Add($"mine={mine.Value.ToString().ToLower()}");
        if (top != 100) qs.Add($"top={top}");
        var url = "api/v1/requests" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");

        var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data").Deserialize<List<RequestDto>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        return data;
    }

    public async Task<RequestDto?> CreateAsync(RequestCreateDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/v1/requests", dto, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return json.TryGetProperty("data", out var d) ? d.Deserialize<RequestDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
    }

    public async Task<RequestDto?> UpdateAsync(int id, RequestUpdateDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/v1/requests/{id}", dto, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return json.TryGetProperty("data", out var d) ? d.Deserialize<RequestDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/v1/requests/{id}", ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<RequestDto?> CompleteAsync(int id, CancellationToken ct = default)
    {
        var resp = await _http.PatchAsync($"api/v1/requests/{id}/complete", null, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return json.TryGetProperty("data", out var d) ? d.Deserialize<RequestDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
    }
}