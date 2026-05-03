using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Admin.DTOs;

namespace PackTracker.Presentation.Services.Admin;

public sealed class AdminApiClient
{
    private readonly IApiClientProvider _apiClientProvider;
    private readonly ILogger<AdminApiClient> _logger;

    public AdminApiClient(IApiClientProvider apiClientProvider, ILogger<AdminApiClient> logger)
    {
        _apiClientProvider = apiClientProvider;
        _logger = logger;
    }

    public async Task<AdminAccessDto?> GetAccessAsync(CancellationToken ct = default)
    {
        return await GetSafeAsync(
            "api/v1/admin/dashboard/access",
            new AdminAccessDto(),
            ct);
    }

    public async Task<AdminDashboardSummaryDto?> GetDashboardAsync(CancellationToken ct = default)
    {
        return await GetSafeAsync<AdminDashboardSummaryDto>(
            "api/v1/admin/dashboard",
            null,
            ct);
    }

    public async Task<AdminSettingsDto?> GetSettingsAsync(CancellationToken ct = default)
    {
        return await GetSafeAsync<AdminSettingsDto>(
            "api/v1/admin/settings",
            null,
            ct);
    }

    public async Task<AdminSettingsDto?> UpdateSettingsAsync(
        UpdateAdminSettingsRequestDto request,
        CancellationToken ct = default)
    {
        return await PutSafeAsync<UpdateAdminSettingsRequestDto, AdminSettingsDto>(
            "api/v1/admin/settings",
            request,
            null,
            ct);
    }

    public async Task<IReadOnlyList<AdminMemberListItemDto>> GetMembersAsync(CancellationToken ct = default)
    {
        return await GetSafeAsync<IReadOnlyList<AdminMemberListItemDto>>(
            "api/v1/admin/members",
            Array.Empty<AdminMemberListItemDto>(),
            ct) ?? Array.Empty<AdminMemberListItemDto>();
    }

    public async Task<IReadOnlyList<AdminRoleOptionDto>> GetAdminRolesAsync(CancellationToken ct = default)
    {
        return await GetSafeAsync<IReadOnlyList<AdminRoleOptionDto>>(
            "api/v1/admin/members/roles",
            Array.Empty<AdminRoleOptionDto>(),
            ct) ?? Array.Empty<AdminRoleOptionDto>();
    }

    public async Task AssignAdminRoleAsync(AssignAdminRoleRequestDto request, CancellationToken ct = default)
    {
        await PostSafeAsync(
            "api/v1/admin/members/assign-role",
            request,
            ct);
    }

    public async Task RevokeAdminRoleAsync(RevokeAdminRoleRequestDto request, CancellationToken ct = default)
    {
        await PostSafeAsync(
            "api/v1/admin/members/revoke-role",
            request,
            ct);
    }

    public async Task<AdminMedalsDto> GetMedalsAsync(CancellationToken ct = default)
    {
        return await GetSafeAsync(
            "api/v1/admin/medals",
            new AdminMedalsDto(
                Array.Empty<AdminMedalDefinitionDto>(),
                Array.Empty<AdminMedalAwardDto>()),
            ct) ?? new AdminMedalsDto(
                Array.Empty<AdminMedalDefinitionDto>(),
                Array.Empty<AdminMedalAwardDto>());
    }

    public async Task<ImportMedalsResultDto> ImportMedalsAsync(
        ImportMedalsRequestDto request,
        CancellationToken ct = default)
    {
        return await PostSafeAsync<ImportMedalsRequestDto, ImportMedalsResultDto>(
            "api/v1/admin/medals/import",
            request,
            new ImportMedalsResultDto(
                0,
                0,
                0,
                0,
                Array.Empty<string>(),
                Array.Empty<string>()),
            ct);
    }

    public async Task<IReadOnlyList<AdminRequestHistoryItemDto>> GetAssistanceRequestHistoryAsync(CancellationToken ct = default)
    {
        return await GetSafeAsync<IReadOnlyList<AdminRequestHistoryItemDto>>(
            "api/v1/admin/requests/history/assistance",
            Array.Empty<AdminRequestHistoryItemDto>(),
            ct) ?? Array.Empty<AdminRequestHistoryItemDto>();
    }

    public async Task<IReadOnlyList<AdminRequestHistoryItemDto>> GetCraftingRequestHistoryAsync(CancellationToken ct = default)
    {
        return await GetSafeAsync<IReadOnlyList<AdminRequestHistoryItemDto>>(
            "api/v1/admin/requests/history/crafting",
            Array.Empty<AdminRequestHistoryItemDto>(),
            ct) ?? Array.Empty<AdminRequestHistoryItemDto>();
    }

    public async Task<IReadOnlyList<AdminRequestHistoryItemDto>> GetProcurementRequestHistoryAsync(CancellationToken ct = default)
    {
        return await GetSafeAsync<IReadOnlyList<AdminRequestHistoryItemDto>>(
            "api/v1/admin/requests/history/procurement",
            Array.Empty<AdminRequestHistoryItemDto>(),
            ct) ?? Array.Empty<AdminRequestHistoryItemDto>();
    }

    public Task<AdminRequestDetailDto?> GetRequestDetailAsync(Guid id, string requestType, CancellationToken ct = default) =>
        GetSafeAsync<AdminRequestDetailDto>(
            $"api/v1/admin/requests/history/{requestType.ToLowerInvariant()}/{id}",
            null,
            ct);

    public async Task<IReadOnlyList<MedalNominationDto>> GetNominationsAsync(CancellationToken ct = default)
    {
        return await GetSafeAsync<IReadOnlyList<MedalNominationDto>>(
            "api/v1/admin/nominations",
            Array.Empty<MedalNominationDto>(),
            ct) ?? Array.Empty<MedalNominationDto>();
    }

    public Task<MedalNominationDto?> SubmitNominationAsync(SubmitMedalNominationRequestDto request, CancellationToken ct = default) =>
        PostNullableSafeAsync<SubmitMedalNominationRequestDto, MedalNominationDto>(
            "api/v1/admin/nominations",
            request,
            ct);

    public Task<AwardRibbonResultDto?> AwardRibbonAsync(AwardRibbonRequestDto request, CancellationToken ct = default) =>
        PostNullableSafeAsync<AwardRibbonRequestDto, AwardRibbonResultDto>(
            "api/v1/admin/medals/award-ribbon",
            request,
            ct);

    public Task<MedalNominationDto?> ApproveNominationAsync(Guid id, ReviewMedalNominationRequestDto request, CancellationToken ct = default) =>
        PostNullableSafeAsync<ReviewMedalNominationRequestDto, MedalNominationDto>(
            $"api/v1/admin/nominations/{id}/approve",
            request,
            ct);

    public Task<MedalNominationDto?> DenyNominationAsync(Guid id, ReviewMedalNominationRequestDto request, CancellationToken ct = default) =>
        PostNullableSafeAsync<ReviewMedalNominationRequestDto, MedalNominationDto>(
            $"api/v1/admin/nominations/{id}/deny",
            request,
            ct);

    private async Task<T?> GetSafeAsync<T>(string endpoint, T? fallback, CancellationToken ct)
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();

            _logger.LogInformation("Calling GET {Endpoint}", endpoint);

            using var response = await client.GetAsync(endpoint, ct);

            _logger.LogInformation(
                "GET {Endpoint} responded with {StatusCode}",
                endpoint,
                response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                LogNonSuccess("GET", endpoint, response.StatusCode);
                return fallback;
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct) ?? fallback;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("GET {Endpoint} was cancelled.", endpoint);
            return fallback;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET {Endpoint} failed.", endpoint);
            return fallback;
        }
    }

    private async Task PostSafeAsync<TRequest>(string endpoint, TRequest request, CancellationToken ct)
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();

            _logger.LogInformation("Calling POST {Endpoint}", endpoint);

            using var response = await client.PostAsJsonAsync(endpoint, request, ct);

            _logger.LogInformation(
                "POST {Endpoint} responded with {StatusCode}",
                endpoint,
                response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                LogNonSuccess("POST", endpoint, response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("POST {Endpoint} was cancelled.", endpoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Endpoint} failed.", endpoint);
        }
    }

    private async Task<TResponse> PostSafeAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        TResponse fallback,
        CancellationToken ct)
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();

            _logger.LogInformation("Calling POST {Endpoint}", endpoint);

            using var response = await client.PostAsJsonAsync(endpoint, request, ct);

            _logger.LogInformation(
                "POST {Endpoint} responded with {StatusCode}",
                endpoint,
                response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                LogNonSuccess("POST", endpoint, response.StatusCode);
                return fallback;
            }

            return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: ct) ?? fallback;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("POST {Endpoint} was cancelled.", endpoint);
            return fallback;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Endpoint} failed.", endpoint);
            return fallback;
        }
    }

    private async Task<TResponse?> PostNullableSafeAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken ct)
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();

            _logger.LogInformation("Calling POST {Endpoint}", endpoint);

            using var response = await client.PostAsJsonAsync(endpoint, request, ct);

            _logger.LogInformation(
                "POST {Endpoint} responded with {StatusCode}",
                endpoint,
                response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                LogNonSuccess("POST", endpoint, response.StatusCode);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("POST {Endpoint} was cancelled.", endpoint);
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Endpoint} failed.", endpoint);
            return default;
        }
    }

    private async Task<TResponse?> PutSafeAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        TResponse? fallback,
        CancellationToken ct)
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();

            _logger.LogInformation("Calling PUT {Endpoint}", endpoint);

            using var response = await client.PutAsJsonAsync(endpoint, request, ct);

            _logger.LogInformation(
                "PUT {Endpoint} responded with {StatusCode}",
                endpoint,
                response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                LogNonSuccess("PUT", endpoint, response.StatusCode);
                return fallback;
            }

            return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: ct) ?? fallback;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("PUT {Endpoint} was cancelled.", endpoint);
            return fallback;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PUT {Endpoint} failed.", endpoint);
            return fallback;
        }
    }

    private void LogNonSuccess(string method, string endpoint, HttpStatusCode statusCode)
    {
        if (statusCode == HttpStatusCode.NotFound)
        {
            _logger.LogError(
                "{Method} {Endpoint} returned 404 Not Found. Check that the backend route exists and matches the client endpoint.",
                method,
                endpoint);
            return;
        }

        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _logger.LogWarning(
                "{Method} {Endpoint} returned {StatusCode}. Treating as no admin access.",
                method,
                endpoint,
                statusCode);
            return;
        }

        _logger.LogError(
            "{Method} {Endpoint} returned unexpected status code {StatusCode}.",
            method,
            endpoint,
            statusCode);
    }
}
