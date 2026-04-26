using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.DTOs.Request;
using PackTracker.Domain.Enums;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

/// <summary>
/// Status constants to avoid magic strings and typos across the VM logic.
/// </summary>
public class RequestStatus
{
    public const string Open = "Open";
    public const string Accepted = "Accepted";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public partial class CraftingRequestsViewModel : ObservableObject
{
    private readonly IApiClientProvider _apiClientProvider;
    private readonly SignalRChatService _signalR;
    private readonly ILogger<CraftingRequestsViewModel> _logger;
    private readonly AvatarCacheService _avatarCache;

    private string? _currentRequestRoomId;
    private string _currentUsername = string.Empty;
    private bool _isCurrentUserModerator;
    private CancellationTokenSource? _switchRoomCts;

    public ObservableCollection<CraftingRequestListItemDto> Requests { get; } = new();
    public ObservableCollection<RequestCommentDto> Comments { get; } = new();
    public ObservableCollection<BlueprintRecipeMaterialDto> RequiredMaterials { get; } = new();
    public ObservableCollection<ChatMessageViewModel> LiveChat { get; } = new();

    [ObservableProperty] private CraftingRequestListItemDto? selectedRequest;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = "Ready";
    [ObservableProperty] private string newCommentText = string.Empty;
    [ObservableProperty] private string liveChatInput = string.Empty;
    [ObservableProperty] private string liveChatStatusText = "Select a request to open the live channel.";
    [ObservableProperty] private bool isLiveChatCounterpartOnline;

    // Logic updated to use Status Constants
    public bool CanAssign => SelectedRequest?.Status == RequestStatus.Open;
    public bool CanMarkInProgress => SelectedRequest?.Status == RequestStatus.Accepted;
    public bool CanMarkCompleted => SelectedRequest is not null
        && (SelectedRequest.Status == RequestStatus.Accepted || SelectedRequest.Status == RequestStatus.InProgress)
        && string.Equals(SelectedRequest.RequesterUsername, _currentUsername, StringComparison.OrdinalIgnoreCase);
    public bool CanCancel => SelectedRequest is not null && SelectedRequest.Status != RequestStatus.Completed && SelectedRequest.Status != RequestStatus.Cancelled;

    public CraftingRequestsViewModel(
        IApiClientProvider apiClientProvider,
        SignalRChatService signalR,
        ILogger<CraftingRequestsViewModel> logger,
        AvatarCacheService avatarCache)
    {
        _apiClientProvider = apiClientProvider;
        _signalR = signalR;
        _logger = logger;
        _avatarCache = avatarCache;
        
        _ = LoadCurrentUserAsync();
        _ = ConnectSignalRAsync();
    }

    private async Task ConnectSignalRAsync()
    {
        try
        {
            await _signalR.ConnectAsync();
            _signalR.RequestMessageReceived += OnRequestMessageReceived;
            _signalR.CraftingRequestCreated += id => _ = RefreshAsync();
            _signalR.CraftingRequestUpdated += id => _ = RefreshAsync();
            _signalR.PresenceUpdated += _ =>
                System.Windows.Application.Current.Dispatcher.BeginInvoke(RefreshPresenceState);
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "ConnectSignalR");
            StatusMessage = $"Chat connection failed: {ex.Message}";
        }
    }

    private void OnRequestMessageReceived(ChatMessageDto msg)
    {
        if (!string.Equals(msg.Channel, _currentRequestRoomId, StringComparison.OrdinalIgnoreCase))
            return;

        var vm = BuildLiveChatVm(msg.Id, msg.Channel, msg.Sender, msg.SenderDisplayName, msg.Content, msg.SentAt, msg.AvatarUrl, msg.SenderRole);
        // Use BeginInvoke to prevent UI thread blocking during high-frequency messaging
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() => LiveChat.Add(vm));
    }

    private ChatMessageViewModel BuildLiveChatVm(
        string id,
        string channel,
        string sender,
        string senderDisplayName,
        string content,
        DateTime sentAt,
        string? avatarUrl,
        string? senderRole = null)
    {
        var isOwn = string.Equals(sender, _currentUsername, StringComparison.OrdinalIgnoreCase);
        var vm = new ChatMessageViewModel
        {
            Id = id,
            Sender = sender,
            SenderDisplayName = senderDisplayName,
            SenderRole = senderRole,
            Content = content,
            SentAt = sentAt,
            IsOwnMessage = isOwn,
            AvatarUrl = avatarUrl
        };

        if (isOwn)
        {
            vm.BeginEditCommand = new PackTracker.Presentation.Commands.RelayCommand(() =>
            {
                vm.EditDraft = vm.Content;
                vm.IsEditing = true;
            });
            vm.ConfirmEditCommand = new PackTracker.Presentation.Commands.RelayCommand(() =>
            {
                var trimmed = vm.EditDraft?.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) && trimmed != vm.Content)
                {
                    _ = _signalR.EditMessageAsync(channel, vm.Id, trimmed);
                    vm.Content = trimmed;
                    vm.IsEdited = true;
                }
                vm.IsEditing = false;
            });
            vm.CancelEditCommand = new PackTracker.Presentation.Commands.RelayCommand(() => vm.IsEditing = false);
            vm.DeleteCommand = new PackTracker.Presentation.Commands.RelayCommand(() => _ = _signalR.DeleteMessageAsync(channel, vm.Id));
        }
        else if (_isCurrentUserModerator)
        {
            vm.DeleteCommand = new PackTracker.Presentation.Commands.RelayCommand(() => _ = _signalR.DeleteMessageAsync(channel, vm.Id));
        }

        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            _ = Task.Run(async () =>
            {
                var img = await _avatarCache.GetAvatarAsync(avatarUrl);
                if (img != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        vm.AvatarImage = img;
                    });
                }
            });
        }

        return vm;
    }

    partial void OnSelectedRequestChanged(CraftingRequestListItemDto? value)
    {
        // Trigger command re-evaluation for button enabled state
        AssignToSelfCommand.NotifyCanExecuteChanged(); ;
        MarkInProgressCommand.NotifyCanExecuteChanged();
        MarkCompletedCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();

        // Trigger visibility binding updates
        OnPropertyChanged(nameof(CanAssign));
        OnPropertyChanged(nameof(CanMarkInProgress));
        OnPropertyChanged(nameof(CanMarkCompleted));
        OnPropertyChanged(nameof(CanCancel));

        RequiredMaterials.Clear();
        if (value?.Materials != null)
        {
            foreach (var m in value.Materials)
                RequiredMaterials.Add(m);
        }

        // Cancel any pending room switches if the user clicks a different item quickly
        _switchRoomCts?.Cancel();
        _switchRoomCts = new CancellationTokenSource();

        if (value is not null)
        {
            _ = LoadCommentsAsync(value.Id);
            _ = SwitchRequestRoomAsync(value.Id.ToString(), _switchRoomCts.Token);
            RefreshPresenceState();
        }
        else
        {
            Comments.Clear();
            _ = SwitchRequestRoomAsync(null, _switchRoomCts.Token);
            LiveChatStatusText = "Select a request to open the live channel.";
            IsLiveChatCounterpartOnline = false;
        }
    }

    private async Task SwitchRequestRoomAsync(string? newRequestId, CancellationToken ct)
    {
        try 
        {
            if (_currentRequestRoomId != null)
                await _signalR.LeaveRequestRoomAsync(_currentRequestRoomId);

            if (ct.IsCancellationRequested) return;

            LiveChat.Clear();
            _currentRequestRoomId = newRequestId;

            if (newRequestId != null)
            {
                await LoadLiveChatAsync(Guid.Parse(newRequestId), ct);
                await _signalR.JoinRequestRoomAsync(newRequestId);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(
                ex,
                "SwitchRequestRoom",
                ("PreviousRoomId", _currentRequestRoomId),
                ("NewRoomId", newRequestId));
            StatusMessage = $"Room switch error: {ex.Message}";
        }
    }

    private async Task LoadLiveChatAsync(Guid requestId, CancellationToken ct)
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();
            using var response = await client.GetAsync($"api/v1/crafting/requests/{requestId}/live-chat", ct);
            if (!response.IsSuccessStatusCode || ct.IsCancellationRequested)
                return;

            var items = await response.Content.ReadFromJsonAsync<List<RequestCommentDto>>(ct)
                        ?? new List<RequestCommentDto>();

            if (ct.IsCancellationRequested)
                return;

            LiveChat.Clear();
            foreach (var item in items)
            {
                var vm = BuildLiveChatVm(
                    item.Id.ToString(),
                    requestId.ToString(),
                    item.AuthorUsername,
                    item.AuthorUsername,
                    item.Content,
                    item.CreatedAt,
                    null);
                LiveChat.Add(vm);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "LoadCraftingLiveChat", ("RequestId", requestId));
            StatusMessage = $"Live chat load failed: {ex.Message}";
        }
    }

    private async Task LoadCommentsAsync(Guid requestId)
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();
            var comments = await client.GetFromJsonAsync<List<RequestCommentDto>>($"api/v1/crafting/requests/{requestId}/comments");
            
            Comments.Clear();
            if (comments != null)
            {
                foreach (var c in comments)
                    Comments.Add(c);
            }
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "LoadComments", ("RequestId", requestId));
            StatusMessage = $"Comment load failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SendLiveChatAsync()
    {
        if (string.IsNullOrWhiteSpace(LiveChatInput) || _currentRequestRoomId is null)
            return;

        var content = LiveChatInput.Trim();
        LiveChatInput = string.Empty;
        await _signalR.SendRequestMessageAsync(_currentRequestRoomId, content);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Refreshing...";

            using var client = _apiClientProvider.CreateClient();
            using var response = await client.GetAsync("api/v1/crafting/requests");

            if (!response.IsSuccessStatusCode)
            {
                var errorDetail = await TryReadErrorDetailAsync(response);
                StatusMessage = string.IsNullOrWhiteSpace(errorDetail)
                    ? $"Server Error: {(int)response.StatusCode}"
                    : $"Server Error: {(int)response.StatusCode} - {errorDetail}";
                return;
            }

            var items = await response.Content.ReadFromJsonAsync<List<CraftingRequestListItemDto>>()
                        ?? new List<CraftingRequestListItemDto>();

            Requests.Clear();
            foreach (var item in items)
                Requests.Add(item);

            StatusMessage = Requests.Count == 0 ? "No requests found." : $"Loaded {Requests.Count} requests.";
            RefreshPresenceState();
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "RefreshCraftingRequests");
            StatusMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public Task RefreshDataAsync() => RefreshAsync();

    private static async Task<string?> TryReadErrorDetailAsync(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            return content.Trim();
        }
        catch
        {
            return null;
        }
    }

    private async Task LoadCurrentUserAsync()
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();

            using var profileResponse = await client.GetAsync("api/v1/profiles/me");
            if (profileResponse.IsSuccessStatusCode)
            {
                var profile = await profileResponse.Content.ReadFromJsonAsync<CurrentUserDto>();
                _currentUsername = profile?.Username ?? string.Empty;
                _isCurrentUserModerator = PackTracker.Domain.Security.SecurityConstants.IsElevatedRequestRole(profile?.DiscordRank);
            }

            using var onlineResponse = await client.GetAsync("api/v1/profiles/online");
            if (onlineResponse.IsSuccessStatusCode)
            {
                var onlineUsers = await onlineResponse.Content.ReadFromJsonAsync<List<OnlineProfileDto>>()
                                  ?? new List<OnlineProfileDto>();
                _signalR.UpdateOnlineUsersSnapshot(onlineUsers.Select(x => x.Username));
            }
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "LoadCurrentCraftingUser");
        }
        finally
        {
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task AddCommentAsync()
    {
        if (SelectedRequest is null || string.IsNullOrWhiteSpace(NewCommentText)) return;

        try
        {
            using var client = _apiClientProvider.CreateClient();
            var response = await client.PostAsJsonAsync($"api/v1/crafting/requests/{SelectedRequest.Id}/comments", 
                new AddRequestCommentDto { Content = NewCommentText });

            if (response.IsSuccessStatusCode)
            {
                NewCommentText = string.Empty;
                await LoadCommentsAsync(SelectedRequest.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "AddCraftingComment", ("RequestId", SelectedRequest.Id));
            StatusMessage = $"Comment failed: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanAssign))]
    private async Task AssignToSelfAsync()
    {
        if (SelectedRequest is null) return;
        await PatchAsync($"api/v1/crafting/requests/{SelectedRequest.Id}/assign", null,
            $"Accepted request.");
    }
    
    [RelayCommand]
    private async Task RequestMaterialAsync(BlueprintRecipeMaterialDto material)
    {
        if (SelectedRequest is null || material is null) return;

        try
        {
            IsLoading = true;
            using var client = _apiClientProvider.CreateClient();
            var dto = new CreateMaterialProcurementRequestDto
            {
                MaterialId = material.MaterialId,
                LinkedCraftingRequestId = SelectedRequest.Id,
                QuantityRequested = (decimal)material.QuantityRequired,
                Priority = RequestPriority.Normal,
                Notes = $"Auto-spawned for: {SelectedRequest.CraftedItemName}"
            };

            var response = await client.PostAsJsonAsync("api/v1/crafting/procurement-requests", dto);
            if (response.IsSuccessStatusCode)
            {
                await client.PostAsJsonAsync($"api/v1/crafting/requests/{SelectedRequest.Id}/comments", 
                    new AddRequestCommentDto { Content = $"[System] Spawned procurement for {material.MaterialName}." });
                await LoadCommentsAsync(SelectedRequest.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(
                ex,
                "CreateProcurementRequest",
                ("RequestId", SelectedRequest.Id),
                ("MaterialId", material.MaterialId),
                ("MaterialName", material.MaterialName));
            StatusMessage = $"Procurement failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanMarkInProgress))]
    private async Task MarkInProgressAsync() => await ChangeStatus(RequestStatus.InProgress, "In progress.");

    [RelayCommand(CanExecute = nameof(CanMarkCompleted))]
    private async Task MarkCompletedAsync() => await ChangeStatus(RequestStatus.Completed, "Completed.");

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private async Task CancelAsync()
    {
        if (SelectedRequest is null) return;

        try
        {
            IsLoading = true;
            using var client = _apiClientProvider.CreateClient();
            using var response = await client.DeleteAsync($"api/v1/crafting/requests/{SelectedRequest.Id}");

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "Request removed.";
                await RefreshAsync();
            }
            else
            {
                var errorDetail = await TryReadErrorDetailAsync(response);
                StatusMessage = string.IsNullOrWhiteSpace(errorDetail)
                    ? $"Update failed ({(int)response.StatusCode})"
                    : $"Update failed ({(int)response.StatusCode}): {errorDetail}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(ex, "CancelCraftingRequest", ("RequestId", SelectedRequest.Id));
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ChangeStatus(string status, string msg)
    {
        if (SelectedRequest is null) return;
        await PatchAsync($"api/v1/crafting/requests/{SelectedRequest.Id}/status", 
            new UpdateRequestStatusDto { Status = status }, msg);
    }

    private async Task PatchAsync(string url, object? body, string successMessage)
    {
        try
        {
            IsLoading = true;
            using var client = _apiClientProvider.CreateClient();
            var response = body is null 
                ? await client.PatchAsync(url, null) 
                : await client.PatchAsJsonAsync(url, body);

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = successMessage;
                await RefreshAsync();
            }
            else
            {
                StatusMessage = $"Update failed ({(int)response.StatusCode})";
            }
        }
        catch (Exception ex)
        {
            _logger.LogViewModelError(
                ex,
                "PatchCraftingRequest",
                ("RequestUrl", url),
                ("RequestId", SelectedRequest?.Id),
                ("HasBody", body is not null));
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshPresenceState()
    {
        var selected = SelectedRequest;
        if (selected is null)
        {
            LiveChatStatusText = "Select a request to open the live channel.";
            IsLiveChatCounterpartOnline = false;
            return;
        }

        var counterpartUsername = ResolveLiveChatCounterpartUsername(selected);
        if (string.IsNullOrWhiteSpace(counterpartUsername))
        {
            LiveChatStatusText = "Waiting for a crafter to accept this request.";
            IsLiveChatCounterpartOnline = false;
            return;
        }

        var isOnline = _signalR.IsUserOnline(counterpartUsername);
        IsLiveChatCounterpartOnline = isOnline;
        LiveChatStatusText = isOnline
            ? $"{counterpartUsername} is live now."
            : $"{counterpartUsername} is offline. Messages will persist and sync when they reconnect.";
    }

    private string? ResolveLiveChatCounterpartUsername(CraftingRequestListItemDto request)
    {
        if (!string.IsNullOrWhiteSpace(_currentUsername)
            && string.Equals(request.RequesterUsername, _currentUsername, StringComparison.OrdinalIgnoreCase))
        {
            return request.AssignedCrafterUsername;
        }

        return request.RequesterUsername;
    }

    private sealed record CurrentUserDto(string Username, string? DiscordRank);
    private sealed record OnlineProfileDto(string Username);
}
