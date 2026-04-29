using System.Collections.ObjectModel;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Presentation.Services.Admin;

namespace PackTracker.Presentation.ViewModels.Admin;

public sealed class AdminMembersViewModel : ViewModelBase
{
    private readonly AdminApiClient _api;
    private AdminMemberListItemDto? _selectedMember;
    private AdminRoleOptionDto? _selectedRole;
    private string _statusMessage = string.Empty;

    public ObservableCollection<AdminMemberListItemDto> Members { get; } = new();
    public ObservableCollection<AdminRoleOptionDto> Roles { get; } = new();

    public AdminMemberListItemDto? SelectedMember
    {
        get => _selectedMember;
        set => SetProperty(ref _selectedMember, value);
    }

    public AdminRoleOptionDto? SelectedRole
    {
        get => _selectedRole;
        set => SetProperty(ref _selectedRole, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public AdminMembersViewModel(AdminApiClient api)
    {
        _api = api;
    }

    public async Task LoadAsync()
    {
        Members.Clear();
        Roles.Clear();

        foreach (var member in await _api.GetMembersAsync())
        {
            Members.Add(member);
        }

        foreach (var role in await _api.GetAdminRolesAsync())
        {
            Roles.Add(role);
        }

        StatusMessage = $"Loaded {Members.Count} members.";
    }

    public async Task AssignSelectedRoleAsync()
    {
        if (SelectedMember is null || SelectedRole is null)
        {
            StatusMessage = "Select a member and role first.";
            return;
        }

        await _api.AssignAdminRoleAsync(new AssignAdminRoleRequestDto(SelectedMember.ProfileId, SelectedRole.RoleId, null));
        await LoadAsync();
        SelectedMember = Members.FirstOrDefault(x => x.ProfileId == SelectedMember.ProfileId);
        StatusMessage = $"Assigned {SelectedRole.Name}.";
    }

    public async Task RevokeSelectedRoleAsync()
    {
        if (SelectedMember is null || SelectedRole is null)
        {
            StatusMessage = "Select a member and role first.";
            return;
        }

        await _api.RevokeAdminRoleAsync(new RevokeAdminRoleRequestDto(SelectedMember.ProfileId, SelectedRole.RoleId, null));
        await LoadAsync();
        SelectedMember = Members.FirstOrDefault(x => x.ProfileId == SelectedMember.ProfileId);
        StatusMessage = $"Revoked {SelectedRole.Name}.";
    }
}
