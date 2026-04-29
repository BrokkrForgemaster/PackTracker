using PackTracker.Application.Admin.DTOs;
using PackTracker.Presentation.Services.Admin;

namespace PackTracker.Presentation.ViewModels.Admin;

public sealed class AdminSettingsViewModel : ViewModelBase
{
    private readonly AdminApiClient _api;
    private bool _operationsEnabled;
    private bool _medalAnnouncementsEnabled;
    private bool _recruitingPostsEnabled;
    private string? _operationsChannelId;
    private string? _medalAnnouncementsChannelId;
    private string? _recruitingPostsChannelId;
    private string _statusMessage = string.Empty;

    public bool OperationsEnabled
    {
        get => _operationsEnabled;
        set => SetProperty(ref _operationsEnabled, value);
    }

    public bool MedalAnnouncementsEnabled
    {
        get => _medalAnnouncementsEnabled;
        set => SetProperty(ref _medalAnnouncementsEnabled, value);
    }

    public bool RecruitingPostsEnabled
    {
        get => _recruitingPostsEnabled;
        set => SetProperty(ref _recruitingPostsEnabled, value);
    }

    public string? OperationsChannelId
    {
        get => _operationsChannelId;
        set => SetProperty(ref _operationsChannelId, value);
    }

    public string? MedalAnnouncementsChannelId
    {
        get => _medalAnnouncementsChannelId;
        set => SetProperty(ref _medalAnnouncementsChannelId, value);
    }

    public string? RecruitingPostsChannelId
    {
        get => _recruitingPostsChannelId;
        set => SetProperty(ref _recruitingPostsChannelId, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public AdminSettingsViewModel(AdminApiClient api)
    {
        _api = api;
    }

    public async Task LoadAsync()
    {
        var dto = await _api.GetSettingsAsync();
        if (dto is null)
        {
            StatusMessage = "Unable to load admin settings.";
            return;
        }

        OperationsEnabled = dto.OperationsEnabled;
        MedalAnnouncementsEnabled = dto.MedalAnnouncementsEnabled;
        RecruitingPostsEnabled = dto.RecruitingPostsEnabled;
        OperationsChannelId = dto.OperationsChannelId;
        MedalAnnouncementsChannelId = dto.MedalAnnouncementsChannelId;
        RecruitingPostsChannelId = dto.RecruitingPostsChannelId;
        StatusMessage = $"Loaded {dto.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC";
    }

    public async Task SaveAsync()
    {
        var dto = await _api.UpdateSettingsAsync(new UpdateAdminSettingsRequestDto(
            OperationsEnabled,
            MedalAnnouncementsEnabled,
            RecruitingPostsEnabled,
            OperationsChannelId,
            MedalAnnouncementsChannelId,
            RecruitingPostsChannelId));

        StatusMessage = dto is null
            ? "Unable to save admin settings."
            : $"Saved {dto.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC";
    }
}
