using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Request;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Infrastructure.Services;

/// <summary>
/// Provides client-side access to the general requests API.
/// </summary>
public class RequestsService : IRequestsService
{
    #region Fields

    private readonly HttpClient _http;
    private readonly ILogger<RequestsService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestsService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    public RequestsService(
        IHttpClientFactory httpClientFactory,
        ILogger<RequestsService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _http = httpClientFactory.CreateClient("default");
        _logger = logger;
    }

    #endregion

    #region Query

    /// <inheritdoc />
    public async Task<List<RequestTicketDto>> QueryAsync(
        RequestStatus? status = null,
        RequestKind? kind = null,
        bool? mine = null,
        int top = 100,
        CancellationToken ct = default)
    {
        var url = BuildQueryUrl(status, kind, mine, top);

        _logger.LogInformation(
            "Querying request tickets. Status={Status} Kind={Kind} Mine={Mine} Top={Top} Url={Url}",
            status, kind, mine, top, url);

        using var response = await _http.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await TryReadBodyAsync(response, ct);

            _logger.LogWarning(
                "Query request tickets failed. StatusCode={StatusCode} Reason={ReasonPhrase} Body={Body}",
                (int)response.StatusCode,
                response.ReasonPhrase,
                errorBody);

            response.EnsureSuccessStatusCode();
        }

        var payload = await response.Content.ReadFromJsonAsync<ApiListResponse<RequestTicketDto>>(JsonOptions, ct);
        var data = payload?.Data ?? new List<RequestTicketDto>();

        _logger.LogInformation("Request tickets query completed. Count={Count}", data.Count);

        return data;
    }

    #endregion

    #region Create

    /// <inheritdoc />
    public async Task<RequestTicketDto?> CreateAsync(RequestCreateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        _logger.LogInformation(
            "Creating request ticket. Title={Title} Kind={Kind} Priority={Priority}",
            dto.Title,
            dto.Kind,
            dto.Priority);

        using var response = await _http.PostAsJsonAsync("api/v1/requests", dto, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await TryReadBodyAsync(response, ct);

            _logger.LogWarning(
                "Create request ticket failed. StatusCode={StatusCode} Reason={ReasonPhrase} Body={Body}",
                (int)response.StatusCode,
                response.ReasonPhrase,
                errorBody);

            response.EnsureSuccessStatusCode();
        }

        var payload = await response.Content.ReadFromJsonAsync<ApiSingleResponse<RequestTicketDto>>(JsonOptions, ct);

        _logger.LogInformation("Request ticket created successfully. Id={Id}", payload?.Data?.Id);

        return payload?.Data;
    }

    #endregion

    #region Update

    /// <inheritdoc />
    public async Task<RequestTicketDto?> UpdateAsync(int id, RequestUpdateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        _logger.LogInformation("Updating request ticket. Id={Id}", id);

        using var response = await _http.PutAsJsonAsync($"api/v1/requests/{id}", dto, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await TryReadBodyAsync(response, ct);

            _logger.LogWarning(
                "Update request ticket failed. Id={Id} StatusCode={StatusCode} Reason={ReasonPhrase} Body={Body}",
                id,
                (int)response.StatusCode,
                response.ReasonPhrase,
                errorBody);

            response.EnsureSuccessStatusCode();
        }

        var payload = await response.Content.ReadFromJsonAsync<ApiSingleResponse<RequestTicketDto>>(JsonOptions, ct);

        _logger.LogInformation("Request ticket updated successfully. Id={Id}", id);

        return payload?.Data;
    }

    #endregion

    #region Delete

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting request ticket. Id={Id}", id);

        using var response = await _http.DeleteAsync($"api/v1/requests/{id}", ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await TryReadBodyAsync(response, ct);

            _logger.LogWarning(
                "Delete request ticket failed. Id={Id} StatusCode={StatusCode} Reason={ReasonPhrase} Body={Body}",
                id,
                (int)response.StatusCode,
                response.ReasonPhrase,
                errorBody);

            return false;
        }

        _logger.LogInformation("Request ticket deleted successfully. Id={Id}", id);

        return true;
    }

    #endregion

    #region Complete

    /// <inheritdoc />
    public async Task<RequestTicketDto?> CompleteAsync(int id, CancellationToken ct = default)
    {
        _logger.LogInformation("Completing request ticket. Id={Id}", id);

        using var response = await _http.PatchAsync($"api/v1/requests/{id}/complete", null, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await TryReadBodyAsync(response, ct);

            _logger.LogWarning(
                "Complete request ticket failed. Id={Id} StatusCode={StatusCode} Reason={ReasonPhrase} Body={Body}",
                id,
                (int)response.StatusCode,
                response.ReasonPhrase,
                errorBody);

            response.EnsureSuccessStatusCode();
        }

        var payload = await response.Content.ReadFromJsonAsync<ApiSingleResponse<RequestTicketDto>>(JsonOptions, ct);

        _logger.LogInformation("Request ticket completed successfully. Id={Id}", id);

        return payload?.Data;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Builds the request query URL using the supplied filters.
    /// </summary>
    private static string BuildQueryUrl(
        RequestStatus? status,
        RequestKind? kind,
        bool? mine,
        int top)
    {
        var queryParts = new List<string>();

        if (status.HasValue)
            queryParts.Add($"status={(int)status.Value}");

        if (kind.HasValue)
            queryParts.Add($"kind={(int)kind.Value}");

        if (mine.HasValue)
            queryParts.Add($"mine={mine.Value.ToString().ToLowerInvariant()}");

        if (top != 100)
            queryParts.Add($"top={top}");

        return queryParts.Count == 0
            ? "api/v1/requests"
            : $"api/v1/requests?{string.Join("&", queryParts)}";
    }

    /// <summary>
    /// Attempts to read the response body as a string for logging purposes.
    /// </summary>
    private static async Task<string> TryReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch
        {
            return "<unable to read response body>";
        }
    }

    #endregion

    #region API Wrapper Models

    /// <summary>
    /// Represents the wrapped API response for a single item.
    /// </summary>
    private sealed class ApiSingleResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
    }

    /// <summary>
    /// Represents the wrapped API response for a list of items.
    /// </summary>
    private sealed class ApiListResponse<T>
    {
        public bool Success { get; set; }
        public int Count { get; set; }
        public List<T> Data { get; set; } = new();
    }

    #endregion
}