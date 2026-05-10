using System.Collections.ObjectModel;
using System.Net.Http.Json;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Mobile.Services;

namespace PackTracker.Mobile.Pages;

public partial class AdminPage : ContentPage
{
    private readonly PackTrackerApiClient _api;
    private readonly ObservableCollection<AdminHistoryCard> _history = new();
    private readonly ObservableCollection<AdminMemberCard> _members = new();
    private readonly ObservableCollection<AdminRoleChoice> _roles = new();
    private readonly ObservableCollection<MedalChoice> _medals = new();
    private readonly ObservableCollection<MedalChoice> _ribbons = new();
    private readonly ObservableCollection<AwardCard> _awards = new();
    private readonly ObservableCollection<NominationCard> _pendingNominations = new();
    private readonly ObservableCollection<AuditLogCard> _auditLogs = new();
    private bool _canAccessAdmin;
    private Guid? _selectedMemberId;
    private Guid? _selectedNominationId;
    private AdminHistoryCard? _selectedHistory;
    private AuditLogCard? _selectedAuditLog;

    public AdminPage(PackTrackerApiClient api)
    {
        InitializeComponent();
        _api = api;
        HistoryView.ItemsSource = _history;
        MembersView.ItemsSource = _members;
        MemberPicker.ItemsSource = _members;
        RolePicker.ItemsSource = _roles;
        MedalPicker.ItemsSource = _medals;
        RibbonPicker.ItemsSource = _ribbons;
        AwardsView.ItemsSource = _awards;
        PendingNominationsView.ItemsSource = _pendingNominations;
        AuditLogsView.ItemsSource = _auditLogs;
        HistoryTypePicker.ItemsSource = new List<string>
        {
            "assistance",
            "crafting",
            "procurement"
        };
        HistoryTypePicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAdminAsync().ConfigureAwait(false);
    }

    private async void RefreshButton_Clicked(object sender, EventArgs e)
    {
        await LoadAdminAsync().ConfigureAwait(false);
    }

    private async void HistoryTypePicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_canAccessAdmin)
            await LoadHistoryAsync().ConfigureAwait(false);
    }

    private void MemberPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (MemberPicker.SelectedItem is AdminMemberCard member)
        {
            _selectedMemberId = member.ProfileId;
            SelectedMemberLabel.Text = $"{member.Name} - {member.Summary}";
            if (string.IsNullOrWhiteSpace(NomineeNameEntry.Text))
                NomineeNameEntry.Text = member.Name;
            return;
        }

        _selectedMemberId = null;
        SelectedMemberLabel.Text = "No member selected.";
    }

    private void PendingNominationsView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not NominationCard nomination)
        {
            _selectedNominationId = null;
            SelectedNominationLabel.Text = "No nomination selected.";
            return;
        }

        _selectedNominationId = nomination.Id;
        SelectedNominationLabel.Text = $"{nomination.Title} - {nomination.Summary}";
    }

    private void HistoryView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedHistory = e.CurrentSelection.FirstOrDefault() as AdminHistoryCard;
        SelectedHistoryLabel.Text = _selectedHistory is null
            ? "No request selected."
            : $"{_selectedHistory.Title} - {_selectedHistory.Summary}";
    }

    private void AuditLogsView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedAuditLog = e.CurrentSelection.FirstOrDefault() as AuditLogCard;
        SelectedAuditLabel.Text = _selectedAuditLog is null
            ? "No audit log selected."
            : $"{_selectedAuditLog.Title} - {_selectedAuditLog.Summary}";
    }

    private async void SaveSettingsButton_Clicked(object sender, EventArgs e)
    {
        if (!_canAccessAdmin)
            return;

        try
        {
            StatusLabel.Text = "Saving admin settings...";
            using var response = await _api.PutAsync(
                "api/v1/admin/settings",
                new UpdateAdminSettingsRequestDto(
                    OperationsEnabledSwitch.IsToggled,
                    MedalAnnouncementsEnabledSwitch.IsToggled,
                    RecruitingPostsEnabledSwitch.IsToggled,
                    NullIfWhiteSpace(OperationsChannelEntry.Text),
                    NullIfWhiteSpace(MedalAnnouncementsChannelEntry.Text),
                    NullIfWhiteSpace(RecruitingPostsChannelEntry.Text)))
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var message = await _api.ReadMessageAsync(response).ConfigureAwait(false);
                await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Settings save failed: {message}");
                return;
            }

            var settings = await response.Content.ReadFromJsonAsync<AdminSettingsDto>().ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ApplySettings(settings);
                StatusLabel.Text = settings is null
                    ? "Settings saved."
                    : $"Settings saved: {settings.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Settings save failed: {ex.Message}");
        }
    }

    private async void AssignRoleButton_Clicked(object sender, EventArgs e)
    {
        await UpdateMemberRoleAsync(assign: true).ConfigureAwait(false);
    }

    private async void RevokeRoleButton_Clicked(object sender, EventArgs e)
    {
        await UpdateMemberRoleAsync(assign: false).ConfigureAwait(false);
    }

    private async void SubmitNominationButton_Clicked(object sender, EventArgs e)
    {
        if (!_canAccessAdmin)
            return;

        if (MedalPicker.SelectedItem is not MedalChoice medal)
        {
            StatusLabel.Text = "Select a medal first.";
            return;
        }

        var nomineeName = !string.IsNullOrWhiteSpace(NomineeNameEntry.Text)
            ? NomineeNameEntry.Text.Trim()
            : MemberPicker.SelectedItem is AdminMemberCard selectedMember
                ? selectedMember.Name
                : string.Empty;

        if (string.IsNullOrWhiteSpace(nomineeName))
        {
            StatusLabel.Text = "Select a member or enter a nominee name.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NominationCitationEditor.Text))
        {
            StatusLabel.Text = "Enter a nomination citation.";
            return;
        }

        try
        {
            StatusLabel.Text = "Submitting nomination...";
            using var response = await _api.PostAsync(
                "api/v1/admin/nominations",
                new SubmitMedalNominationRequestDto(
                    medal.Id,
                    _selectedMemberId,
                    nomineeName,
                    NominationCitationEditor.Text.Trim()))
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var message = await _api.ReadMessageAsync(response).ConfigureAwait(false);
                await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Nomination failed: {message}");
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                NominationCitationEditor.Text = string.Empty;
                StatusLabel.Text = $"Nomination submitted for {nomineeName}.";
            });
            await LoadAdminAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Nomination failed: {ex.Message}");
        }
    }

    private async void AwardRibbonButton_Clicked(object sender, EventArgs e)
    {
        if (!_canAccessAdmin)
            return;

        if (_selectedMemberId is null)
        {
            StatusLabel.Text = "Select a member before awarding a ribbon.";
            return;
        }

        if (RibbonPicker.SelectedItem is not MedalChoice ribbon)
        {
            StatusLabel.Text = "Select a ribbon first.";
            return;
        }

        try
        {
            StatusLabel.Text = "Awarding ribbon...";
            using var response = await _api.PostAsync(
                "api/v1/admin/medals/award-ribbon",
                new AwardRibbonRequestDto(
                    ribbon.Name,
                    ribbon.Description,
                    ribbon.ImagePath,
                    ribbon.PublicImageUrl,
                    [_selectedMemberId.Value],
                    NullIfWhiteSpace(RibbonCitationEditor.Text)))
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var message = await _api.ReadMessageAsync(response).ConfigureAwait(false);
                await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Ribbon award failed: {message}");
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<AwardRibbonResultDto>().ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                RibbonCitationEditor.Text = string.Empty;
                StatusLabel.Text = result?.AlreadyAwarded == true
                    ? $"{result.RecipientName} already has {result.RibbonName}."
                    : $"{result?.RibbonName ?? ribbon.Name} awarded.";
            });
            await LoadAdminAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Ribbon award failed: {ex.Message}");
        }
    }

    private async void ApproveNominationButton_Clicked(object sender, EventArgs e)
    {
        await ReviewNominationAsync(approve: true).ConfigureAwait(false);
    }

    private async void DenyNominationButton_Clicked(object sender, EventArgs e)
    {
        await ReviewNominationAsync(approve: false).ConfigureAwait(false);
    }

    private async void LoadRequestDetailButton_Clicked(object sender, EventArgs e)
    {
        if (_selectedHistory is null)
        {
            StatusLabel.Text = "Select a request first.";
            return;
        }

        try
        {
            StatusLabel.Text = "Loading request detail...";
            var detail = await _api.GetAsync<AdminRequestDetailDto>(
                $"api/v1/admin/requests/history/{_selectedHistory.RequestType}/{_selectedHistory.Id}").ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (detail is null)
                {
                    RequestDetailTitleLabel.Text = "Request detail unavailable.";
                    RequestDetailMetaLabel.Text = "-";
                    RequestDetailDescriptionLabel.Text = "-";
                    RequestDetailClaimsLabel.Text = "-";
                    StatusLabel.Text = "Request detail not found.";
                    return;
                }

                RequestDetailTitleLabel.Text = detail.Title;
                RequestDetailMetaLabel.Text =
                    $"{detail.RequestType} | {detail.Status} | {detail.Priority} | Requester {detail.RequesterDisplayName ?? "Unknown"} | Assignee {detail.AssigneeDisplayName ?? "Unassigned"}";
                RequestDetailDescriptionLabel.Text = string.IsNullOrWhiteSpace(detail.Description)
                    ? detail.RefusalReason ?? "No description."
                    : detail.Description;
                RequestDetailClaimsLabel.Text = detail.Claims.Count == 0
                    ? "Claims: none"
                    : $"Claims: {string.Join(" | ", detail.Claims.Select(x => $"{x.DisplayName} {x.ClaimedAt:yyyy-MM-dd HH:mm}"))}";
                StatusLabel.Text = "Request detail loaded.";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Request detail failed: {ex.Message}");
        }
    }

    private async void RefreshAuditLogsButton_Clicked(object sender, EventArgs e)
    {
        await LoadAuditLogsAsync().ConfigureAwait(false);
    }

    private async void LoadAuditDetailButton_Clicked(object sender, EventArgs e)
    {
        if (_selectedAuditLog is null)
        {
            StatusLabel.Text = "Select an audit log first.";
            return;
        }

        try
        {
            StatusLabel.Text = "Loading audit detail...";
            var detail = await _api.GetAsync<AdminAuditLogDetailDto>(
                $"api/v1/admin/auditlogs/{_selectedAuditLog.Id}").ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (detail is null)
                {
                    AuditDetailTitleLabel.Text = "Audit detail unavailable.";
                    AuditDetailMetaLabel.Text = "-";
                    AuditDetailExceptionLabel.Text = "-";
                    StatusLabel.Text = "Audit detail not found.";
                    return;
                }

                AuditDetailTitleLabel.Text = detail.Summary;
                AuditDetailMetaLabel.Text =
                    $"{detail.Severity} | {detail.ActorDisplayName} | {detail.Action} | {detail.OccurredAt:yyyy-MM-dd HH:mm:ss} | {detail.TargetType}:{detail.TargetId}";
                AuditDetailExceptionLabel.Text = FirstNonEmpty(
                    detail.Exception,
                    detail.AfterJson,
                    detail.BeforeJson,
                    "No extended detail.");
                StatusLabel.Text = "Audit detail loaded.";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Audit detail failed: {ex.Message}");
        }
    }

    private async Task LoadAdminAsync()
    {
        try
        {
            StatusLabel.Text = "Checking access...";
            var access = await _api.GetAsync<AdminAccessDto>("api/v1/admin/dashboard/access").ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _canAccessAdmin = access?.CanAccessAdmin == true;
                AccessLabel.Text = access?.CanAccessAdmin == true
                    ? $"Admin enabled | Tier {access.HighestTier ?? "Unknown"}"
                    : "This user cannot access admin.";
                RolesLabel.Text = access is null
                    ? "No access data."
                    : $"Roles: {string.Join(", ", access.Roles)}";
            });

            if (!_canAccessAdmin)
            {
                await MainThread.InvokeOnMainThreadAsync(ResetAdminState);
                return;
            }

            var dashboardTask = _api.GetAsync<AdminDashboardSummaryDto>("api/v1/admin/dashboard");
            var settingsTask = _api.GetAsync<AdminSettingsDto>("api/v1/admin/settings");
            var membersTask = _api.GetAsync<List<AdminMemberListItemDto>>("api/v1/admin/members");
            var rolesTask = _api.GetAsync<List<AdminRoleOptionDto>>("api/v1/admin/members/roles");
            var medalsTask = _api.GetAsync<AdminMedalsDto>("api/v1/admin/medals");
            var nominationsTask = _api.GetAsync<List<MedalNominationDto>>("api/v1/admin/nominations");
            var auditLogsTask = _api.GetAsync<List<AdminAuditLogListItemDto>>("api/v1/admin/auditlogs?take=50");

            await Task.WhenAll(dashboardTask, settingsTask, membersTask, rolesTask, medalsTask, nominationsTask, auditLogsTask).ConfigureAwait(false);

            var dashboard = await dashboardTask.ConfigureAwait(false);
            var settings = await settingsTask.ConfigureAwait(false);
            var members = await membersTask.ConfigureAwait(false);
            var roles = await rolesTask.ConfigureAwait(false);
            var medals = await medalsTask.ConfigureAwait(false);
            var nominations = await nominationsTask.ConfigureAwait(false);
            var auditLogs = await auditLogsTask.ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                DashboardLabel.Text = dashboard is null
                    ? "Dashboard unavailable."
                    : $"Members {dashboard.TotalMembers} | Admin roles {dashboard.ActiveAdminRoleAssignments} | Audit entries {dashboard.TotalAuditEntries}";
                SettingsSummaryLabel.Text = settings is null
                    ? "Settings unavailable."
                    : $"Ops {(settings.OperationsEnabled ? "On" : "Off")} | Medals {(settings.MedalAnnouncementsEnabled ? "On" : "Off")} | Recruiting {(settings.RecruitingPostsEnabled ? "On" : "Off")}";

                ApplySettings(settings);
                ApplyMembers(members);
                ApplyRoles(roles);
                ApplyMedals(medals);
                ApplyNominations(nominations);
                ApplyAuditLogs(auditLogs);
                RestoreSelectedMember();
                RestoreSelectedNomination();
                RestoreSelectedAudit();
                StatusLabel.Text = "Admin data loaded.";
            });

            await LoadHistoryAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = $"Admin load failed: {ex.Message}";
            });
        }
    }

    private async Task LoadHistoryAsync()
    {
        if (!_canAccessAdmin)
            return;

        try
        {
            var type = HistoryTypePicker.SelectedItem?.ToString() ?? "assistance";
            var items = await _api.GetAsync<List<AdminRequestHistoryItemDto>>(
                $"api/v1/admin/requests/history/{type}").ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _history.Clear();
                if (items is not null)
                {
                    foreach (var item in items.Take(30))
                        _history.Add(new AdminHistoryCard(item));
                }

                _selectedHistory = null;
                HistoryView.SelectedItem = null;
                SelectedHistoryLabel.Text = "No request selected.";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = $"Admin history failed: {ex.Message}";
            });
        }
    }

    private async Task LoadAuditLogsAsync()
    {
        if (!_canAccessAdmin)
            return;

        try
        {
            StatusLabel.Text = "Loading audit logs...";
            var logs = await _api.GetAsync<List<AdminAuditLogListItemDto>>("api/v1/admin/auditlogs?take=50").ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ApplyAuditLogs(logs);
                RestoreSelectedAudit();
                StatusLabel.Text = "Audit logs loaded.";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Audit logs failed: {ex.Message}");
        }
    }

    private async Task UpdateMemberRoleAsync(bool assign)
    {
        if (!_canAccessAdmin)
            return;

        if (_selectedMemberId is null || RolePicker.SelectedItem is not AdminRoleChoice role)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = "Select a member and role first.";
            });
            return;
        }

        try
        {
            StatusLabel.Text = assign ? "Assigning role..." : "Revoking role...";
            using var response = assign
                ? await _api.PostAsync(
                    "api/v1/admin/members/assign-role",
                    new AssignAdminRoleRequestDto(_selectedMemberId.Value, role.RoleId, null)).ConfigureAwait(false)
                : await _api.PostAsync(
                    "api/v1/admin/members/revoke-role",
                    new RevokeAdminRoleRequestDto(_selectedMemberId.Value, role.RoleId, null)).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var message = await _api.ReadMessageAsync(response).ConfigureAwait(false);
                await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Role update failed: {message}");
                return;
            }

            await LoadAdminAsync().ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = assign
                    ? $"Assigned {role.DisplayName}."
                    : $"Revoked {role.DisplayName}.";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Role update failed: {ex.Message}");
        }
    }

    private async Task ReviewNominationAsync(bool approve)
    {
        if (!_canAccessAdmin)
            return;

        if (_selectedNominationId is null)
        {
            StatusLabel.Text = "Select a nomination first.";
            return;
        }

        try
        {
            StatusLabel.Text = approve ? "Approving nomination..." : "Denying nomination...";
            using var response = await _api.PostAsync(
                approve
                    ? $"api/v1/admin/nominations/{_selectedNominationId.Value}/approve"
                    : $"api/v1/admin/nominations/{_selectedNominationId.Value}/deny",
                new ReviewMedalNominationRequestDto(NullIfWhiteSpace(NominationReviewNotesEditor.Text)))
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var message = await _api.ReadMessageAsync(response).ConfigureAwait(false);
                await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Nomination review failed: {message}");
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                NominationReviewNotesEditor.Text = string.Empty;
                StatusLabel.Text = approve ? "Nomination approved." : "Nomination denied.";
            });
            await LoadAdminAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = $"Nomination review failed: {ex.Message}");
        }
    }

    private void ResetAdminState()
    {
        DashboardLabel.Text = "-";
        SettingsSummaryLabel.Text = "-";
        MedalSummaryLabel.Text = "-";
        NominationSummaryLabel.Text = "-";
        SelectedMemberLabel.Text = "No member selected.";
        SelectedNominationLabel.Text = "No nomination selected.";
        SelectedHistoryLabel.Text = "No request selected.";
        SelectedAuditLabel.Text = "No audit log selected.";
        RequestDetailTitleLabel.Text = "-";
        RequestDetailMetaLabel.Text = "-";
        RequestDetailDescriptionLabel.Text = "-";
        RequestDetailClaimsLabel.Text = "-";
        AuditDetailTitleLabel.Text = "-";
        AuditDetailMetaLabel.Text = "-";
        AuditDetailExceptionLabel.Text = "-";
        _history.Clear();
        _members.Clear();
        _roles.Clear();
        _medals.Clear();
        _ribbons.Clear();
        _awards.Clear();
        _pendingNominations.Clear();
        _auditLogs.Clear();
        StatusLabel.Text = "Admin access denied.";
    }

    private void ApplySettings(AdminSettingsDto? settings)
    {
        OperationsEnabledSwitch.IsToggled = settings?.OperationsEnabled == true;
        MedalAnnouncementsEnabledSwitch.IsToggled = settings?.MedalAnnouncementsEnabled == true;
        RecruitingPostsEnabledSwitch.IsToggled = settings?.RecruitingPostsEnabled == true;
        OperationsChannelEntry.Text = settings?.OperationsChannelId ?? string.Empty;
        MedalAnnouncementsChannelEntry.Text = settings?.MedalAnnouncementsChannelId ?? string.Empty;
        RecruitingPostsChannelEntry.Text = settings?.RecruitingPostsChannelId ?? string.Empty;
    }

    private void ApplyMembers(List<AdminMemberListItemDto>? members)
    {
        _members.Clear();
        if (members is null)
            return;

        foreach (var member in members.Take(25))
            _members.Add(new AdminMemberCard(member));
    }

    private void ApplyRoles(List<AdminRoleOptionDto>? roles)
    {
        _roles.Clear();
        if (roles is null)
            return;

        foreach (var role in roles)
            _roles.Add(new AdminRoleChoice(role));
    }

    private void ApplyMedals(AdminMedalsDto? medals)
    {
        _medals.Clear();
        _ribbons.Clear();
        _awards.Clear();

        var available = medals?.AvailableMedals ?? Array.Empty<AdminMedalDefinitionDto>();
        foreach (var medal in available)
        {
            if (string.Equals(medal.AwardType, "Ribbon", StringComparison.OrdinalIgnoreCase))
                _ribbons.Add(new MedalChoice(medal));
            else
                _medals.Add(new MedalChoice(medal));
        }

        foreach (var award in medals?.Awards?.Take(25) ?? Array.Empty<AdminMedalAwardDto>())
            _awards.Add(new AwardCard(award));

        MedalSummaryLabel.Text = $"Medals {_medals.Count} | Ribbons {_ribbons.Count} | Awards {_awards.Count}";
    }

    private void ApplyNominations(List<MedalNominationDto>? nominations)
    {
        _pendingNominations.Clear();
        var pending = nominations?
            .Where(x => string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .Select(x => new NominationCard(x))
            .ToList() ?? [];

        foreach (var nomination in pending)
            _pendingNominations.Add(nomination);

        var pendingCount = nominations?.Count(x => string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase)) ?? 0;
        var historyCount = nominations?.Count - pendingCount ?? 0;
        NominationSummaryLabel.Text = $"Pending {pendingCount} | Reviewed {historyCount}";
    }

    private void ApplyAuditLogs(List<AdminAuditLogListItemDto>? logs)
    {
        _auditLogs.Clear();
        if (logs is null)
            return;

        foreach (var log in logs.Take(30))
            _auditLogs.Add(new AuditLogCard(log));
    }

    private void RestoreSelectedMember()
    {
        if (_selectedMemberId is null)
        {
            MemberPicker.SelectedItem = null;
            SelectedMemberLabel.Text = "No member selected.";
            return;
        }

        var match = _members.FirstOrDefault(x => x.ProfileId == _selectedMemberId.Value);
        MemberPicker.SelectedItem = match;
        SelectedMemberLabel.Text = match is null
            ? "No member selected."
            : $"{match.Name} - {match.Summary}";
    }

    private void RestoreSelectedNomination()
    {
        if (_selectedNominationId is null)
        {
            PendingNominationsView.SelectedItem = null;
            SelectedNominationLabel.Text = "No nomination selected.";
            return;
        }

        var match = _pendingNominations.FirstOrDefault(x => x.Id == _selectedNominationId.Value);
        PendingNominationsView.SelectedItem = match;
        SelectedNominationLabel.Text = match is null
            ? "No nomination selected."
            : $"{match.Title} - {match.Summary}";
    }

    private void RestoreSelectedAudit()
    {
        if (_selectedAuditLog is null)
        {
            AuditLogsView.SelectedItem = null;
            SelectedAuditLabel.Text = "No audit log selected.";
            return;
        }

        var match = _auditLogs.FirstOrDefault(x => x.Id == _selectedAuditLog.Id);
        _selectedAuditLog = match;
        AuditLogsView.SelectedItem = match;
        SelectedAuditLabel.Text = match is null
            ? "No audit log selected."
            : $"{match.Title} - {match.Summary}";
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatRoles(IReadOnlyCollection<string> roles) =>
        roles.Count == 0 ? "None" : string.Join(", ", roles);

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private sealed class AdminHistoryCard
    {
        public AdminHistoryCard(AdminRequestHistoryItemDto item)
        {
            Id = item.Id;
            RequestType = item.RequestType.ToLowerInvariant();
            Title = item.Title;
            Summary = $"{item.RequestType} | {item.Status} | {item.Priority} | {item.RequesterDisplayName ?? "Unknown"}";
        }

        public Guid Id { get; }
        public string RequestType { get; }
        public string Title { get; }
        public string Summary { get; }
    }

    private sealed class AdminMemberCard
    {
        public AdminMemberCard(AdminMemberListItemDto item)
        {
            ProfileId = item.ProfileId;
            Name = item.DisplayName ?? item.Username;
            Summary = $"{item.DiscordRank ?? "Unknown rank"} | {item.DiscordDivision ?? "Unknown division"} | Roles: {FormatRoles(item.ActiveAdminRoles)}";
            DisplayName = $"{Name} ({item.Username})";
        }

        public Guid ProfileId { get; }
        public string Name { get; }
        public string Summary { get; }
        public string DisplayName { get; }
    }

    private sealed class AdminRoleChoice
    {
        public AdminRoleChoice(AdminRoleOptionDto role)
        {
            RoleId = role.RoleId;
            DisplayName = $"{role.Name} ({role.Tier})";
        }

        public Guid RoleId { get; }
        public string DisplayName { get; }
    }

    private sealed class MedalChoice
    {
        public MedalChoice(AdminMedalDefinitionDto medal)
        {
            Id = medal.Id;
            Name = medal.Name;
            Description = medal.Description;
            ImagePath = medal.ImagePath;
            PublicImageUrl = medal.PublicImageUrl;
            DisplayName = $"{medal.Name} ({medal.AwardCount})";
        }

        public Guid Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string? ImagePath { get; }
        public string? PublicImageUrl { get; }
        public string DisplayName { get; }
    }

    private sealed class AwardCard
    {
        public AwardCard(AdminMedalAwardDto award)
        {
            Title = $"{award.MedalName} -> {award.ProfileDisplayName ?? award.RecipientName}";
            Summary = $"{award.SourceSystem} | {(award.AwardedAt ?? award.ImportedAt):yyyy-MM-dd}";
        }

        public string Title { get; }
        public string Summary { get; }
    }

    private sealed class NominationCard
    {
        public NominationCard(MedalNominationDto nomination)
        {
            Id = nomination.Id;
            Title = $"{nomination.MedalName} for {nomination.NomineeName}";
            Summary = $"{nomination.NominatorName} | {nomination.SubmittedAt:yyyy-MM-dd} | {nomination.Citation}";
        }

        public Guid Id { get; }
        public string Title { get; }
        public string Summary { get; }
    }

    private sealed class AuditLogCard
    {
        public AuditLogCard(AdminAuditLogListItemDto log)
        {
            Id = log.Id;
            Title = $"{log.Severity} | {log.Action}";
            Summary = $"{log.ActorDisplayName} | {log.OccurredAt:yyyy-MM-dd HH:mm:ss} | {log.Summary}";
        }

        public Guid Id { get; }
        public string Title { get; }
        public string Summary { get; }
    }
}
