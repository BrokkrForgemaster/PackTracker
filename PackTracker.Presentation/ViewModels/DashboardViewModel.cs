using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using PackTracker.Application.DTOs.Dashboard;
using PackTracker.Presentation.Commands;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly IApiClientProvider _apiClientProvider;
    private readonly SignalRChatService _signalR;
    
    private int _nextZIndex = 1;
    private bool _isConnected = true;
    private string? _currentUserDisplayName = "Loading...";
    private string? _currentUserRole = "Member";

    public DashboardViewModel(
        IApiClientProvider apiClientProvider,
        SignalRChatService signalR,
        GuideDashboardViewModel guideViewModel)
    {
        _apiClientProvider = apiClientProvider;
        _signalR = signalR;
        Guide = guideViewModel;

        AvailableChatChannels = new ObservableCollection<AvailableChannelViewModel>();
        OpenChatWindows = new ObservableCollection<ChatWindowViewModel>();
        CollapsedWindowsWithUnread = new ObservableCollection<ChatWindowViewModel>();
        OnlineUsers = new ObservableCollection<OnlineUserViewModel>();
        ActiveRequests = new ObservableCollection<ActiveRequestDto>();

        OpenChatWindowCommand = new RelayCommand<AvailableChannelViewModel>(OpenChatWindow);
        CascadeChatWindowsCommand = new RelayCommand(CascadeWindows);
        CollapseAllChatWindowsCommand = new RelayCommand(CollapseAllWindows);
        ExpandAllChatWindowsCommand = new RelayCommand(ExpandAllWindows);

        LoadChatChannelsForRole(CurrentUserRole);
        OpenDefaultWindows();

        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        try
        {
            await LoadCurrentUserAsync();
            await _signalR.ConnectAsync();

            // Join any channels whose windows were already opened in the constructor
            foreach (var window in OpenChatWindows)
                await _signalR.JoinChannelAsync(window.ChannelKey);

            await RefreshDataAsync();

            _signalR.MessageReceived += OnMessageReceived;
            _signalR.PresenceUpdated += OnPresenceUpdated;

            _signalR.AssistanceRequestCreated += id => _ = RefreshDataAsync();
            _signalR.AssistanceRequestUpdated += id => _ = RefreshDataAsync();
            _signalR.CraftingRequestCreated += id => _ = RefreshDataAsync();
            _signalR.CraftingRequestUpdated += id => _ = RefreshDataAsync();
            _signalR.ProcurementRequestCreated += id => _ = RefreshDataAsync();
            _signalR.ProcurementRequestUpdated += id => _ = RefreshDataAsync();

            _signalR.ConnectionStateChanged += state =>
            {
                IsConnected = state;
                if (state) _ = RefreshDataAsync();
            };
        }
        catch (Exception)
        {
            // Log or handle
        }
    }

    private async Task LoadCurrentUserAsync()
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();
            var profile = await client.GetFromJsonAsync<CurrentUserDto>("api/v1/profiles/me");
            if (profile != null)
            {
                CurrentUserDisplayName = !string.IsNullOrWhiteSpace(profile.DiscordDisplayName)
                    ? profile.DiscordDisplayName
                    : profile.Username;
                CurrentUserRole = !string.IsNullOrWhiteSpace(profile.DiscordRank)
                    ? profile.DiscordRank
                    : "Member";
                // Rebuild the available channel list for the real role.
                // Already-open windows are unaffected; new role-gated channels
                // become available for the user to open manually.
                LoadChatChannelsForRole(CurrentUserRole);
            }
        }
        catch
        {
            // Non-fatal — fall back to defaults already set in constructor
        }
    }

    private void OnMessageReceived(ChatMessageDto msg)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var window = OpenChatWindows.FirstOrDefault(x => x.ChannelKey == msg.Channel);
            window?.ReceiveMessage(msg.SenderDisplayName, msg.Content);
        });
    }

    private void OnPresenceUpdated(IReadOnlyList<OnlineUserDto> users)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            OnlineUsers.Clear();
            foreach (var u in users)
            {
                OnlineUsers.Add(new OnlineUserViewModel
                {
                    DisplayName = u.DisplayName,
                    Role = u.Role,
                    RoleColorBrush = BrushFromHex("#B89C78")
                });
            }
        });
    }

    public async Task RefreshDataAsync()
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();
            var summary = await client.GetFromJsonAsync<DashboardSummaryDto>("api/v1/dashboard/summary");
            
            if (summary != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ActiveRequests.Clear();
                    foreach (var req in summary.ActiveRequests)
                        ActiveRequests.Add(req);
                    
                    OnPropertyChanged(nameof(TopRequests));
                });

                await Guide.RefreshAsync();
                OnPropertyChanged(nameof(TopGuideRequests));
            }
        }
        catch (Exception)
        {
            // Handle
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public string? CurrentUserDisplayName
    {
        get => _currentUserDisplayName;
        set => SetProperty(ref _currentUserDisplayName, value);
    }

    public string? CurrentUserRole
    {
        get => _currentUserRole;
        set => SetProperty(ref _currentUserRole, value);
    }

    public ObservableCollection<AvailableChannelViewModel> AvailableChatChannels { get; }
    public ObservableCollection<ChatWindowViewModel> OpenChatWindows { get; }
    public ObservableCollection<ChatWindowViewModel> CollapsedWindowsWithUnread { get; }
    public ObservableCollection<OnlineUserViewModel> OnlineUsers { get; }
    public ObservableCollection<ActiveRequestDto> ActiveRequests { get; }
    
    public IEnumerable<ActiveRequestDto> TopRequests => ActiveRequests.Take(5);
    
    public GuideDashboardViewModel Guide { get; }
    public IEnumerable<PackTracker.Domain.Entities.GuideRequest> TopGuideRequests => Guide.Requests.Take(2);

    public ICommand OpenChatWindowCommand { get; }
    public ICommand CascadeChatWindowsCommand { get; }
    public ICommand CollapseAllChatWindowsCommand { get; }
    public ICommand ExpandAllChatWindowsCommand { get; }

    private void LoadChatChannelsForRole(string? role)
    {
        AvailableChatChannels.Clear();

        AddChannel("direct", "Direct Message", "Private conversations", "#6A4F8B");
        AddChannel("general", "General", "All members", "#4F6A84");

        if (role is "LOCOPS" or "Leadership")
            AddChannel("locops", "LOCOPS", "LOCOPS members", "#5C8B5E");

        if (role is "TACOPS" or "Leadership")
            AddChannel("tacops", "TACOPS", "TACOPS members", "#A36E2F");

        if (role is "SPECOPS" or "Leadership")
            AddChannel("specops", "SPECOPS", "SPECOPS members", "#844F4F");

        if (role is "ARCOPS" or "Leadership")
            AddChannel("arcops", "ARCOPS", "ARCOPS members", "#1A6E6E");

        if (role == "Leadership")
            AddChannel("leadership", "Leadership", "Leadership only", "#B090E0");
    }

    private void AddChannel(string key, string name, string access, string hex)
    {
        AvailableChatChannels.Add(new AvailableChannelViewModel
        {
            Key = key,
            DisplayName = name,
            AccessSummary = access,
            AccentBrush = BrushFromHex(hex)
        });
    }

    private void OpenDefaultWindows()
    {
        var general = AvailableChatChannels.FirstOrDefault(x => x.Key == "general");
        if (general != null)
            OpenChatWindow(general);

        var direct = AvailableChatChannels.FirstOrDefault(x => x.Key == "direct");
        if (direct != null)
            OpenChatWindow(direct);

        var division = AvailableChatChannels.FirstOrDefault(x =>
            x.Key is "locops" or "tacops" or "specops" or "arcops");
        if (division != null)
            OpenChatWindow(division);
    }

    private void OpenChatWindow(AvailableChannelViewModel? channel)
    {
        if (channel == null)
            return;

        var existing = OpenChatWindows.FirstOrDefault(x => x.ChannelKey == channel.Key);
        if (existing != null)
        {
            existing.IsCollapsed = false;
            existing.UnreadCount = 0;
            BringToFront(existing);
            RefreshCollapsedAlerts();
            return;
        }

        var index = OpenChatWindows.Count;

        var window = new ChatWindowViewModel(CloseWindow, BringToFront, OnWindowExpanded)
        {
            ChannelKey = channel.Key,
            Title = channel.DisplayName,
            AccentBrush = channel.AccentBrush,
            Left = 30 + (index * 40),
            Top = 25 + (index * 35),
            Width = 380,
            WindowHeight = 420,
            ZIndex = GetNextZIndex()
        };

        window.Messages.CollectionChanged += (_, _) => SyncUnreadState(window, channel);
        window.MessageSent += (channelKey, content) => _ = _signalR.SendMessageAsync(channelKey, content);

        OpenChatWindows.Add(window);
        RefreshCollapsedAlerts();

        if (_signalR.IsConnected)
            _ = _signalR.JoinChannelAsync(channel.Key);
    }

    private void SyncUnreadState(ChatWindowViewModel window, AvailableChannelViewModel channel)
    {
        channel.UnreadCount = window.UnreadCount;
        channel.HasUnread = window.HasUnread;
        RefreshCollapsedAlerts();
    }

    private void CloseWindow(ChatWindowViewModel window)
    {
        var channel = AvailableChatChannels.FirstOrDefault(x => x.Key == window.ChannelKey);
        if (channel != null)
        {
            channel.HasUnread = window.HasUnread;
            channel.UnreadCount = window.UnreadCount;
        }

        OpenChatWindows.Remove(window);
        RefreshCollapsedAlerts();
    }

    private void BringToFront(ChatWindowViewModel window)
    {
        window.ZIndex = GetNextZIndex();
    }

    private void OnWindowExpanded(ChatWindowViewModel window)
    {
        var channel = AvailableChatChannels.FirstOrDefault(x => x.Key == window.ChannelKey);
        if (channel != null)
        {
            channel.HasUnread = false;
            channel.UnreadCount = 0;
        }

        RefreshCollapsedAlerts();
    }

    private void CascadeWindows()
    {
        var x = 30.0;
        var y = 25.0;

        foreach (var window in OpenChatWindows)
        {
            window.Left = x;
            window.Top = y;
            window.IsCollapsed = false;
            window.ZIndex = GetNextZIndex();

            x += 40;
            y += 35;
        }

        RefreshCollapsedAlerts();
    }

    private void CollapseAllWindows()
    {
        foreach (var window in OpenChatWindows)
            window.IsCollapsed = true;

        RefreshCollapsedAlerts();
    }

    private void ExpandAllWindows()
    {
        foreach (var window in OpenChatWindows)
        {
            window.IsCollapsed = false;
            window.UnreadCount = 0;
            window.HasUnread = false;
        }

        foreach (var channel in AvailableChatChannels)
        {
            channel.UnreadCount = 0;
            channel.HasUnread = false;
        }

        RefreshCollapsedAlerts();
    }

    private void RefreshCollapsedAlerts()
    {
        CollapsedWindowsWithUnread.Clear();

        foreach (var window in OpenChatWindows.Where(x => x.IsCollapsed && x.HasUnread))
            CollapsedWindowsWithUnread.Add(window);
    }

    private int GetNextZIndex() => _nextZIndex++;

    private static Brush BrushFromHex(string hex)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
    }

    // optional test helper
    public void SimulateIncomingMessage(string channelKey, string sender, string content)
    {
        var window = OpenChatWindows.FirstOrDefault(x => x.ChannelKey == channelKey);
        if (window == null)
            return;

        window.ReceiveMessage(sender, content);

        var channel = AvailableChatChannels.FirstOrDefault(x => x.Key == channelKey);
        if (channel != null)
        {
            channel.UnreadCount = window.UnreadCount;
            channel.HasUnread = window.HasUnread;
        }

        RefreshCollapsedAlerts();
    }

    private record CurrentUserDto(
        string Username,
        string? DiscordDisplayName,
        string? DiscordRank);
}
