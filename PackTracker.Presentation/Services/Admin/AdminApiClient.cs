using System.Net;
using System.Net.Http.Json;
using PackTracker.Application.Admin.DTOs;

namespace PackTracker.Presentation.Services.Admin;

public sealed class AdminApiClient
{
    private readonly IApiClientProvider _apiClientProvider;

    public AdminApiClient(IApiClientProvider apiClientProvider)
    {
        _apiClientProvider = apiClientProvider;
    }

    public async Task<AdminAccessDto?> GetAccessAsync(CancellationToken ct = default)
    {
        using var client = _apiClientProvider.CreateClient();
        using var response = await client.GetAsync("api/v1/admin/dashboard/access", ct);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AdminAccessDto>(cancellationToken: ct);
    }

    public async Task<AdminDashboardSummaryDto?> GetDashboardAsync(CancellationToken ct = default)
    {
        using var client = _apiClientProvider.CreateClient();
        return await client.GetFromJsonAsync<AdminDashboardSummaryDto>("api/v1/admin/dashboard", ct);
    }

    public async Task<AdminSettingsDto?> GetSettingsAsync(CancellationToken ct = default)
    {
        using var client = _apiClientProvider.CreateClient();
        return await client.GetFromJsonAsync<AdminSettingsDto>("api/v1/admin/settings", ct);
    }

    public async Task<AdminSettingsDto?> UpdateSettingsAsync(UpdateAdminSettingsRequestDto request, CancellationToken ct = default)
    {
        using var client = _apiClientProvider.CreateClient();
        using var response = await client.PutAsJsonAsync("api/v1/admin/settings", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AdminSettingsDto>(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<AdminMemberListItemDto>> GetMembersAsync(CancellationToken ct = default)
    {
        using var client = _apiClientProvider.CreateClient();
        return await client.GetFromJsonAsync<IReadOnlyList<AdminMemberListItemDto>>("api/v1/admin/members", ct)
            ?? Array.Empty<AdminMemberListItemDto>();
    }

    public async Task<IReadOnlyList<AdminRoleOptionDto>> GetAdminRolesAsync(CancellationToken ct = default)
    {
        using var client = _apiClientProvider.CreateClient();
        return await client.GetFromJsonAsync<IReadOnlyList<AdminRoleOptionDto>>("api/v1/admin/members/roles", ct)
            ?? Array.Empty<AdminRoleOptionDto>();
    }

    public async Task AssignAdminRoleAsync(AssignAdminRoleRequestDto request, CancellationToken ct = default)
    {
        using var client = _apiClientProvider.CreateClient();
        using var response = await client.PostAsJsonAsync("api/v1/admin/members/assign-role", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task RevokeAdminRoleAsync(RevokeAdminRoleRequestDto request, CancellationToken ct = default)
    {
        using var client = _apiClientProvider.CreateClient();
        using var response = await client.PostAsJsonAsync("api/v1/admin/members/revoke-role", request, ct);
        response.EnsureSuccessStatusCode();
    }
}
