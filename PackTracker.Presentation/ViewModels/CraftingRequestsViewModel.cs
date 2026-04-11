using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    
    private string? _currentRequestRoomId;
    private CancellationTokenSource? _switchRoomCts;

    public ObservableCollection<CraftingRequestListItemDto> Requests { get; } = new();
    public ObservableCollection<RequestCommentDto> Comments { get; } = new();
    public ObservableCollection<BlueprintRecipeMaterialDto> RequiredMaterials { get; } = new();
    public ObservableCollection<ChatMessageDto> LiveChat { get; } = new();

    [ObservableProperty] private CraftingRequestListItemDto? selectedRequest;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusMessage = "Ready";
    [ObservableProperty] private string newCommentText = string.Empty;
    [ObservableProperty] private string liveChatInput = string.Empty;

    // Logic updated to use Status Constants
    public bool CanAssign => SelectedRequest?.Status == RequestStatus.Open;
    public bool CanMarkInProgress => SelectedRequest?.Status == RequestStatus.Accepted;
    public bool CanMarkCompleted => SelectedRequest is not null && (SelectedRequest.Status == RequestStatus.Accepted || SelectedRequest.Status == RequestStatus.InProgress);
    public bool CanCancel => SelectedRequest is not null && SelectedRequest.Status != RequestStatus.Completed && SelectedRequest.Status != RequestStatus.Cancelled;

    public CraftingRequestsViewModel(IApiClientProvider apiClientProvider, SignalRChatService signalR)
    {
        _apiClientProvider = apiClientProvider;
        _signalR = signalR;
        
        _ = RefreshAsync();
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
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chat connection failed: {ex.Message}";
        }
    }

    private void OnRequestMessageReceived(ChatMessageDto msg)
    {
        // Use BeginInvoke to prevent UI thread blocking during high-frequency messaging
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() => LiveChat.Add(msg));
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
        }
        else
        {
            Comments.Clear();
            _ = SwitchRequestRoomAsync(null, _switchRoomCts.Token);
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
                await _signalR.JoinRequestRoomAsync(newRequestId);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Room switch error: {ex.Message}";
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
                StatusMessage = $"Server Error: {(int)response.StatusCode}";
                return;
            }

            var items = await response.Content.ReadFromJsonAsync<List<CraftingRequestListItemDto>>()
                        ?? new List<CraftingRequestListItemDto>();

            Requests.Clear();
            foreach (var item in items)
                Requests.Add(item);

            StatusMessage = Requests.Count == 0 ? "No requests found." : $"Loaded {Requests.Count} requests.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
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
                StatusMessage = $"Update failed ({(int)response.StatusCode})";
            }
        }
        catch (Exception ex)
        {
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
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
