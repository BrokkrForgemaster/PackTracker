using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PackTracker.Application.DTOs.Dashboard;
using PackTracker.Presentation.Commands;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly IApiClientProvider _apiClientProvider;
    private readonly SignalRChatService _signalR;
    private static readonly Regex DirectMentionPattern = new(
        @"^\s*@(?<username>[A-Za-z0-9_.-]+)\s+(?<message>.+)$",
        RegexOptions.Compiled);
    
    private int _nextZIndex = 1;
    private bool _isConnected = true;
    private string? _currentUserDisplayName = "Loading...";
    private string? _currentUserRole = "Member";
    private bool _chatSoundMuted;
    private string? _currentUsername;
    private ChatWindowViewModel? _activeChatWindow;

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
        FloatingChatWindows = new ObservableCollection<ChatWindowViewModel>();
        MinimizedChatWindows = new ObservableCollection<ChatWindowViewModel>();
        CollapsedWindowsWithUnread = new ObservableCollection<ChatWindowViewModel>();
        OnlineUsers = new ObservableCollection<OnlineUserViewModel>();
        ActiveRequests = new ObservableCollection<ActiveRequestDto>();

        OpenChatWindowCommand = new RelayCommand<AvailableChannelViewModel>(SelectChannel);
        SelectChannelCommand = new RelayCommand<AvailableChannelViewModel>(SelectChannel);
        OpenDirectMessageCommand = new RelayCommand<OnlineUserViewModel>(OpenDirectMessage);
        CascadeChatWindowsCommand = new RelayCommand(CascadeWindows);
        CollapseAllChatWindowsCommand = new RelayCommand(CollapseAllWindows);
        ExpandAllChatWindowsCommand = new RelayCommand(ExpandAllWindows);
        ToggleMuteCommand = new RelayCommand(() => ChatSoundMuted = !ChatSoundMuted);

        LoadChatChannelsForRole(null);
        OpenDefaultWindows();

        _ = InitAsync();
    }

    public ObservableCollection<ChatWindowViewModel> MinimizedChatWindows { get; set; }

    public ObservableCollection<ChatWindowViewModel> FloatingChatWindows { get; set; }

    private async Task InitAsync()
    {
        try
        {
            await LoadCurrentUserAsync();
            await _signalR.ConnectAsync();

            // Subscribe before joining so history messages aren't lost between
            // GetLobbyHistory and the handler being wired up.
            _signalR.MessageReceived += OnMessageReceived;
            _signalR.MessageEdited += OnMessageEdited;
            _signalR.MessageDeleted += OnMessageDeleted;
            _signalR.PresenceUpdated += OnPresenceUpdated;

            // Join any channels whose windows were already opened in the constructor
            foreach (var window in OpenChatWindows)
                await _signalR.JoinChannelAsync(window.ChannelKey);

            await RefreshDataAsync();

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

    public async Task LoadCurrentUserAsync()
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();
            var profile = await client.GetFromJsonAsync<CurrentUserDto>("api/v1/profiles/me");
            if (profile != null)
            {
                _currentUsername = profile.Username;
                CurrentUserDisplayName = !string.IsNullOrWhiteSpace(profile.DiscordDisplayName)
                    ? profile.DiscordDisplayName
                    : profile.Username;
                CurrentUserRole = !string.IsNullOrWhiteSpace(profile.DiscordRank)
                    ? profile.DiscordRank
                    : "Foundling";

                foreach (var window in OpenChatWindows)
                {
                    window.CurrentUsername = _currentUsername;
                    window.CurrentUserDisplayName = CurrentUserDisplayName;
                }

                LoadChatChannelsForRole(profile.DiscordDivision);
            }
        }
        catch
        {
            // Non-fatal — fall back to defaults already set in constructor
        }
    }

    private void OnMessageReceived(ChatMessageDto msg)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            var window = OpenChatWindows.FirstOrDefault(x => x.ChannelKey == msg.Channel);
            if (window == null && msg.Channel.StartsWith("direct:", StringComparison.OrdinalIgnoreCase))
            {
                var username = msg.Channel["direct:".Length..];
                window = EnsureDirectMessageWindow(username, msg.SenderDisplayName);
            }

            if (window != null)
            {
                window.ReceiveMessage(
                    msg.Id,
                    msg.Sender,
                    msg.SenderDisplayName,
                    msg.Content,
                    msg.SentAt,
                    msg.SenderRole);
                PlayNotificationSound(window);
            }
        }));
    }

    private void OnMessageEdited(ChatMessageEditedDto edit)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            var window = OpenChatWindows.FirstOrDefault(x => x.ChannelKey == edit.Channel);
            window?.ApplyEdit(edit.MessageId, edit.NewContent);
        }));
    }

    private void OnMessageDeleted(ChatMessageDeletedDto del)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
        {
            var window = OpenChatWindows.FirstOrDefault(x => x.ChannelKey == del.Channel);
            window?.ApplyDelete(del.MessageId);
        }));
    }

    private static BitmapImage? CreateAvatarImage(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(avatarUrl, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
    private void OnPresenceUpdated(IReadOnlyList<OnlineUserDto> users)
    {
        _ = Task.Run(async () =>
        {
            var vms = new List<(OnlineUserViewModel vm, byte[]? avatarBytes)>();

            foreach (var u in users)
            {
                byte[]? avatarBytes = null;
                if (!string.IsNullOrWhiteSpace(u.AvatarUrl) && Uri.TryCreate(u.AvatarUrl, UriKind.Absolute, out _))
                {
                    try
                    {
                        using var http = new System.Net.Http.HttpClient();
                        http.Timeout = TimeSpan.FromSeconds(10);
                        avatarBytes = await http.GetByteArrayAsync(u.AvatarUrl);
                    }
                    catch { }
                }

                var vm = new OnlineUserViewModel
                {
                    Username = u.Username,
                    ContactLabel = u.DisplayName ?? u.Username,
                    DiscordDisplayName = u.DisplayName,
                    Role = u.Role,
                    RoleColorBrush = OnlineUserViewModel.GetRoleColor(u.Role)
                };
                vms.Add((vm, avatarBytes));
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                OnlineUsers.Clear();
                foreach (var (vm, avatarBytes) in vms)
                {
                    if (avatarBytes != null)
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.StreamSource = new System.IO.MemoryStream(avatarBytes);
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.EndInit();
                            bmp.Freeze();
                            vm.AvatarImage = bmp;
                        }
                        catch { }
                    }
                    OnlineUsers.Add(vm);
                }
            });
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
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    ActiveRequests.Clear();
                    foreach (var req in summary.ActiveRequests)
                        ActiveRequests.Add(req);
                }));

                await Guide.RefreshAsync();
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => OnPropertyChanged(nameof(TopGuideRequests))));
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

    public bool ChatSoundMuted
    {
        get => _chatSoundMuted;
        set => SetProperty(ref _chatSoundMuted, value);
    }

    public ObservableCollection<AvailableChannelViewModel> AvailableChatChannels { get; }
    public ObservableCollection<ChatWindowViewModel> OpenChatWindows { get; }
    public ObservableCollection<ChatWindowViewModel> CollapsedWindowsWithUnread { get; }
    public ObservableCollection<OnlineUserViewModel> OnlineUsers { get; }
    public ObservableCollection<ActiveRequestDto> ActiveRequests { get; }

    public GuideDashboardViewModel Guide { get; }
    public IEnumerable<PackTracker.Domain.Entities.GuideRequest> TopGuideRequests => Guide.Requests.Take(2);

    public ChatWindowViewModel? ActiveChatWindow
    {
        get => _activeChatWindow;
        private set => SetProperty(ref _activeChatWindow, value);
    }

    public ICommand OpenChatWindowCommand { get; }
    public ICommand SelectChannelCommand { get; }
    public ICommand OpenDirectMessageCommand { get; }
    public ICommand CascadeChatWindowsCommand { get; }
    public ICommand CollapseAllChatWindowsCommand { get; }
    public ICommand ExpandAllChatWindowsCommand { get; }
    public ICommand ToggleMuteCommand { get; }

    private void LoadChatChannelsForRole(string? division)
    {
        AvailableChatChannels.Clear();

        AddChannel("direct", "Direct Message", "Private conversations", "#6A4F8B");
        AddChannel("general", "General", "All members", "#4F6A84");

        bool isLeadership = string.Equals(division, "Leadership", StringComparison.OrdinalIgnoreCase);

        if (isLeadership || string.Equals(division, "LOCOPS", StringComparison.OrdinalIgnoreCase))
            AddChannel("locops", "LOCOPS", "LOCOPS members", "#5C8B5E");

        if (isLeadership || string.Equals(division, "TACOPS", StringComparison.OrdinalIgnoreCase))
            AddChannel("tacops", "TACOPS", "TACOPS members", "#A36E2F");

        if (isLeadership || string.Equals(division, "SPECOPS", StringComparison.OrdinalIgnoreCase))
            AddChannel("specops", "SPECOPS", "SPECOPS members", "#844F4F");

        if (isLeadership || string.Equals(division, "ARCOPS", StringComparison.OrdinalIgnoreCase))
            AddChannel("arcops", "ARCOPS", "ARCOPS members", "#1A6E6E");

        if (isLeadership)
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
            SelectChannel(general);
    }

    private void SelectChannel(AvailableChannelViewModel? channel)
    {
        if (channel == null) return;

        foreach (var ch in AvailableChatChannels)
            ch.IsSelected = false;
        channel.IsSelected = true;
        channel.HasUnread = false;
        channel.UnreadCount = 0;

        var existing = OpenChatWindows.FirstOrDefault(x => x.ChannelKey == channel.Key);
        if (existing != null)
        {
            existing.UnreadCount = 0;
            existing.HasUnread = false;
            ActiveChatWindow = existing;
            return;
        }

        var window = new ChatWindowViewModel(CloseWindow, BringToFront, OnWindowStateChanged)
        {
            ChannelKey = channel.Key,
            Title = channel.DisplayName,
            AccentBrush = channel.AccentBrush,
            CurrentUserDisplayName = CurrentUserDisplayName,
            CurrentUsername = _currentUsername,
        };

        window.Messages.CollectionChanged += (_, _) => SyncUnreadState(window, channel);
        window.MessageSent += (s, content) => { _ = SendWindowMessageAsync(window, content); };
        window.EditRequested += (ch, msgId, newContent) => { _ = _signalR.EditMessageAsync(ch, msgId, newContent); };
        window.DeleteRequested += (ch, msgId) => { _ = _signalR.DeleteMessageAsync(ch, msgId); };

        OpenChatWindows.Add(window);

        if (_signalR.IsConnected)
            _ = _signalR.JoinChannelAsync(channel.Key);

        ActiveChatWindow = window;
    }

    private void SyncUnreadState(ChatWindowViewModel window, AvailableChannelViewModel channel)
    {
        if (ActiveChatWindow == window)
        {
            window.UnreadCount = 0;
            window.HasUnread = false;
            channel.UnreadCount = 0;
            channel.HasUnread = false;
        }
        else
        {
            channel.UnreadCount = window.UnreadCount;
            channel.HasUnread = window.HasUnread;
        }
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
        FloatingChatWindows.Remove(window);
        MinimizedChatWindows.Remove(window);
        RefreshCollapsedAlerts();
    }

    private void BringToFront(ChatWindowViewModel window)
    {
        window.ZIndex = GetNextZIndex();
    }

    private void OnWindowStateChanged(ChatWindowViewModel window)
    {
        if (window.IsCollapsed)
        {
            FloatingChatWindows.Remove(window);
            if (!MinimizedChatWindows.Contains(window))
                MinimizedChatWindows.Add(window);
        }
        else
        {
            MinimizedChatWindows.Remove(window);
            if (!FloatingChatWindows.Contains(window))
                FloatingChatWindows.Add(window);

            var channel = AvailableChatChannels.FirstOrDefault(x => x.Key == window.ChannelKey);
            if (channel != null)
            {
                channel.HasUnread = false;
                channel.UnreadCount = 0;
            }
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

    private async Task SendWindowMessageAsync(ChatWindowViewModel window, string content)
    {
        if (window.IsDirectMessage && !string.IsNullOrWhiteSpace(window.TargetUsername))
        {
            await _signalR.SendDirectMessageAsync(window.TargetUsername, content);
            return;
        }

        var mention = DirectMentionPattern.Match(content);
        if (mention.Success)
        {
            var username = mention.Groups["username"].Value;
            var message = mention.Groups["message"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(message))
            {
                EnsureDirectMessageWindow(username, username);
                await _signalR.SendDirectMessageAsync(username, message);
                return;
            }
        }

        await _signalR.SendMessageAsync(window.ChannelKey, content);
    }

    private void OpenDirectMessage(OnlineUserViewModel? user)
    {
        if (user == null || string.IsNullOrWhiteSpace(user.Username))
            return;

        var window = EnsureDirectMessageWindow(user.Username, user.DiscordDisplayName);
        ActiveChatWindow = window;
    }

    private ChatWindowViewModel EnsureDirectMessageWindow(string username, string? displayName)
    {
        var channelKey = BuildDirectChannelKey(username);
        var existing = OpenChatWindows.FirstOrDefault(x => x.ChannelKey == channelKey);
        if (existing != null)
        {
            existing.UnreadCount = 0;
            return existing;
        }

        var window = new ChatWindowViewModel(CloseWindow, BringToFront, OnWindowStateChanged)
        {
            ChannelKey = channelKey,
            Title = $"DM // {displayName ?? username}",
            TargetUsername = username,
            TargetDisplayName = displayName ?? username,
            CurrentUserDisplayName = CurrentUserDisplayName,
            CurrentUsername = _currentUsername,
            AccentBrush = BrushFromHex("#6A4F8B"),
        };

        window.MessageSent += (s, content) => { _ = SendWindowMessageAsync(window, content); };
        window.EditRequested += (ch, msgId, newContent) => { _ = _signalR.EditMessageAsync(ch, msgId, newContent); };
        window.DeleteRequested += (ch, msgId) => { _ = _signalR.DeleteMessageAsync(ch, msgId); };
        OpenChatWindows.Add(window);

        if (_signalR.IsConnected)
            _ = _signalR.JoinChannelAsync(channelKey);

        return window;
    }

    private static string BuildDirectChannelKey(string username) =>
        $"direct:{username.Trim().ToLowerInvariant()}";

    private static Brush BrushFromHex(string hex)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
    }

    private void PlayNotificationSound(ChatWindowViewModel window)
    {
        if (ActiveChatWindow == window)
            return;

        if (!ChatSoundMuted)
        {
            try
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
            catch
            {
                // Non-fatal — sound may not be available
            }
        }

        // Flash the taskbar to draw visual attention
        try
        {
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null && !mainWindow.IsActive)
            {
                FlashWindow(mainWindow);
            }
        }
        catch
        {
            // Non-fatal
        }
    }

    private static void FlashWindow(System.Windows.Window window)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(window);
        var info = new NativeMethods.FLASHWINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.FLASHWINFO>(),
            hwnd = helper.Handle,
            dwFlags = NativeMethods.FLASHW_ALL | NativeMethods.FLASHW_TIMERNOFG,
            uCount = 3,
            dwTimeout = 0
        };
        NativeMethods.FlashWindowEx(ref info);
    }

    // optional test helper
    public void SimulateIncomingMessage(string channelKey, string sender, string content)
    {
        var window = OpenChatWindows.FirstOrDefault(x => x.ChannelKey == channelKey);
        if (window == null)
            return;

        window.ReceiveMessage(Guid.NewGuid().ToString(), sender, sender, content, DateTime.UtcNow);

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
        string? DiscordRank,
        string? DiscordDivision);
}
