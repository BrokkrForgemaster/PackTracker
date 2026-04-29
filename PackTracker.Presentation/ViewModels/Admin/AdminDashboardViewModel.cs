using PackTracker.Presentation.Services.Admin;

namespace PackTracker.Presentation.ViewModels.Admin;

public sealed class AdminDashboardViewModel : ViewModelBase
{
    private readonly AdminApiClient _api;
    private string? _highestTier;
    private int _totalMembers;
    private int _activeAdminRoleAssignments;
    private int _totalAuditEntries;
    private bool _discordSettingsConfigured;

    public string? HighestTier
    {
        get => _highestTier;
        set => SetProperty(ref _highestTier, value);
    }

    public int TotalMembers
    {
        get => _totalMembers;
        set => SetProperty(ref _totalMembers, value);
    }

    public int ActiveAdminRoleAssignments
    {
        get => _activeAdminRoleAssignments;
        set => SetProperty(ref _activeAdminRoleAssignments, value);
    }

    public int TotalAuditEntries
    {
        get => _totalAuditEntries;
        set => SetProperty(ref _totalAuditEntries, value);
    }

    public bool DiscordSettingsConfigured
    {
        get => _discordSettingsConfigured;
        set => SetProperty(ref _discordSettingsConfigured, value);
    }

    public AdminDashboardViewModel(AdminApiClient api)
    {
        _api = api;
    }

    public async Task LoadAsync()
    {
        var dto = await _api.GetDashboardAsync();
        if (dto is null)
        {
            return;
        }

        HighestTier = dto.HighestTier;
        TotalMembers = dto.TotalMembers;
        ActiveAdminRoleAssignments = dto.ActiveAdminRoleAssignments;
        TotalAuditEntries = dto.TotalAuditEntries;
        DiscordSettingsConfigured = dto.DiscordSettingsConfigured;
    }
}
