using System.Collections.ObjectModel;
using System.Net.Http.Json;
using PackTracker.Application.DTOs.Request;
using PackTracker.Domain.Security;
using PackTracker.Mobile.Services;

namespace PackTracker.Mobile.Pages;

public partial class RequestHubPage : ContentPage
{
    private readonly PackTrackerApiClient _api;
    private readonly ObservableCollection<RequestCard> _requests = new();
    private CurrentUserState? _currentUser;

    public RequestHubPage(PackTrackerApiClient api)
    {
        InitializeComponent();
        _api = api;
        RequestsView.ItemsSource = _requests;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadRequestsAsync().ConfigureAwait(false);
    }

    private async void RefreshButton_Clicked(object sender, EventArgs e)
    {
        await LoadRequestsAsync().ConfigureAwait(false);
    }

    private async void ClaimButton_Clicked(object sender, EventArgs e)
    {
        if (((Button)sender).CommandParameter is not RequestCard request)
            return;

        var path = request.Dto.IsClaimedByCurrentUser
            ? $"api/v1/requests/{request.Dto.Id}/unclaim"
            : $"api/v1/requests/{request.Dto.Id}/claim";

        using var response = await _api.PatchAsync(path).ConfigureAwait(false);
        await RefreshWithStatusAsync(response).ConfigureAwait(false);
    }

    private async void CompleteButton_Clicked(object sender, EventArgs e)
    {
        if (((Button)sender).CommandParameter is not RequestCard request)
            return;

        using var response = await _api.PatchAsync($"api/v1/requests/{request.Dto.Id}/complete").ConfigureAwait(false);
        await RefreshWithStatusAsync(response).ConfigureAwait(false);
    }

    private async void PinButton_Clicked(object sender, EventArgs e)
    {
        if (((Button)sender).CommandParameter is not RequestCard request)
            return;

        var path = request.Dto.IsPinned
            ? $"api/v1/requests/{request.Dto.Id}/unpin"
            : $"api/v1/requests/{request.Dto.Id}/pin";

        using var response = await _api.PatchAsync(path).ConfigureAwait(false);
        await RefreshWithStatusAsync(response).ConfigureAwait(false);
    }

    private async void DeleteButton_Clicked(object sender, EventArgs e)
    {
        if (((Button)sender).CommandParameter is not RequestCard request)
            return;

        using var response = await _api.DeleteAsync($"api/v1/requests/{request.Dto.Id}").ConfigureAwait(false);
        await RefreshWithStatusAsync(response).ConfigureAwait(false);
    }

    private async Task RefreshWithStatusAsync(HttpResponseMessage response)
    {
        var message = await _api.ReadMessageAsync(response).ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            StatusLabel.Text = message;
        });

        if (response.IsSuccessStatusCode)
            await LoadRequestsAsync().ConfigureAwait(false);
    }

    private async Task LoadRequestsAsync()
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = "Loading requests...");
            _currentUser = await _api.GetAsync<CurrentUserState>("api/v1/profiles/me").ConfigureAwait(false);
            var requests = await _api.GetAsync<List<AssistanceRequestDto>>("api/v1/requests").ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _requests.Clear();
                if (requests is null || requests.Count == 0)
                {
                    StatusLabel.Text = "No requests found.";
                    return;
                }

                foreach (var request in requests)
                    _requests.Add(new RequestCard(request, _currentUser));

                StatusLabel.Text = $"Loaded {_requests.Count} requests.";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = $"Requests failed: {ex.Message}";
            });
        }
    }

    private sealed record CurrentUserState(string Username, string? DiscordRank);

    private sealed class RequestCard
    {
        public RequestCard(AssistanceRequestDto dto, CurrentUserState? currentUser)
        {
            Dto = dto;
            Title = dto.Title;
            Description = string.IsNullOrWhiteSpace(dto.Description)
                ? "No description."
                : dto.Description;
            Summary = $"{dto.Kind} • {dto.Priority} • {dto.Status} • Claims {dto.ClaimCount}/{dto.MaxClaims}";

            var currentUsername = currentUser?.Username;
            var currentRole = currentUser?.DiscordRank;
            var createdByCurrentUser = !string.IsNullOrWhiteSpace(currentUsername)
                && string.Equals(dto.CreatedByUsername, currentUsername, StringComparison.OrdinalIgnoreCase);
            var claimedByCurrentUser = dto.IsClaimedByCurrentUser
                || (!string.IsNullOrWhiteSpace(currentUsername)
                    && string.Equals(dto.AssignedToUsername, currentUsername, StringComparison.OrdinalIgnoreCase));

            CanClaimOrUnclaim = claimedByCurrentUser
                || (dto.Status == "Open" && dto.ClaimCount < dto.MaxClaims);
            ClaimActionLabel = claimedByCurrentUser ? "UNCLAIM" : "CLAIM";
            CanComplete = createdByCurrentUser && dto.Status is not "Completed" and not "Cancelled";
            CanDelete = createdByCurrentUser || SecurityConstants.IsElevatedRequestRole(currentRole);
            CanPin = SecurityConstants.IsRallyMasterOrAbove(currentRole);
            PinActionLabel = dto.IsPinned ? "UNPIN" : "PIN";
        }

        public AssistanceRequestDto Dto { get; }
        public string Title { get; }
        public string Summary { get; }
        public string Description { get; }
        public bool CanClaimOrUnclaim { get; }
        public string ClaimActionLabel { get; }
        public bool CanComplete { get; }
        public bool CanDelete { get; }
        public bool CanPin { get; }
        public string PinActionLabel { get; }
    }
}
