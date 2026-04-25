using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
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
    private readonly AvatarCacheService _avatarCache;
    private static readonly Regex DirectMentionPattern = new(
        @"^\s*@(?<username>[A-Za-z0-9_.-]+)\s+(?<message>.+)$",
        RegexOptions.Compiled);
    private static readonly System.Text.Json.JsonSerializerOptions DashboardJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    
    private int _nextZIndex = 1;
    private bool _isConnected = true;
    private string? _currentUserDisplayName = "Loading...";
    private string? _currentUserRole = "Member";
    private bool _chatSoundMuted;
    private string? _currentUsername;
    private ChatWindowViewModel? _activeChatWindow;
    private string? _requestLoadError;
    private int _totalNewClaimsCount;
    private readonly Dictionary<Guid, int> _newClaimCounts = new();
    private readonly Dictionary<Guid, int> _lastKnownClaimCounts = new();
    private System.Windows.Threading.DispatcherTimer? _periodicRefreshTimer;

    public DashboardViewModel(
        IApiClientProvider apiClientProvider,
        SignalRChatService signalR,
        GuideDashboardViewModel guideViewModel,
        AvatarCacheService avatarCache,
        DiscordEventsViewModel discordEvents)
    {
        _apiClientProvider = apiClientProvider;
        _signalR = signalR;
        _avatarCache = avatarCache;
        Guide = guideViewModel;
        DiscordEvents = discordEvents;

        AvailableChatChannels = new ObservableCollection<AvailableChannelViewModel>();
        OpenChatWindows = new ObservableCollection<ChatWindowViewModel>();
        FloatingChatWindows = new ObservableCollection<ChatWindowViewModel>();
        MinimizedChatWindows = new ObservableCollection<ChatWindowViewModel>();
        CollapsedWindowsWithUnread = new ObservableCollection<ChatWindowViewModel>();
        AllMembers = new ObservableCollection<OnlineUserViewModel>();
        ActiveRequests = new ObservableCollection<ActiveRequestItemViewModel>();
        PinnedRequests = new ObservableCollection<ActiveRequestItemViewModel>();
        RegularRequests = new ObservableCollection<ActiveRequestItemViewModel>();
        MyActiveTasks = new ObservableCollection<ActiveRequestItemViewModel>();
        MyOpenRequests = new ObservableCollection<ActiveRequestItemViewModel>();

        OpenChatWindowCommand = new RelayCommand<AvailableChannelViewModel>(SelectChannel);
        SelectChannelCommand = new RelayCommand<AvailableChannelViewModel>(SelectChannel);
        OpenDirectMessageCommand = new RelayCommand<OnlineUserViewModel>(OpenDirectMessage);
        CascadeChatWindowsCommand = new RelayCommand(CascadeWindows);
        CollapseAllChatWindowsCommand = new RelayCommand(CollapseAllWindows);
        ExpandAllChatWindowsCommand = new RelayCommand(ExpandAllWindows);
        ToggleMuteCommand = new RelayCommand(() => ChatSoundMuted = !ChatSoundMuted);
        DismissAllNewClaimsCommand = new RelayCommand(DismissAllNewClaims);

        LoadChatChannelsForRole(null);
        OpenDefaultWindows();

        _ = InitAsync();
    }

    public ObservableCollection<ChatWindowViewModel> MinimizedChatWindows { get; }

    public ObservableCollection<ChatWindowViewModel> FloatingChatWindows { get; }

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
            _signalR.PendingDirectMessages += OnPendingDirectMessages;

            // Join any channels whose windows were already opened in the constructor
            foreach (var window in OpenChatWindows)
                await _signalR.JoinChannelAsync(window.ChannelKey);

            // Wire up request-change and claim handlers BEFORE the initial refresh so any
            // SignalR event that arrives during the HTTP call is captured rather than dropped.
            _signalR.AssistanceRequestCreated += id => _ = RefreshDataAsync();
            _signalR.AssistanceRequestUpdated += id => _ = RefreshDataAsync();
            _signalR.CraftingRequestCreated += id => _ = RefreshDataAsync();
            _signalR.CraftingRequestUpdated += id => _ = RefreshDataAsync();
            _signalR.ProcurementRequestCreated += id => _ = RefreshDataAsync();
            _signalR.ProcurementRequestUpdated += id => _ = RefreshDataAsync();
            _signalR.RequestClaimed += OnClaimConfirmed;

            _signalR.ConnectionStateChanged += state =>
            {
                IsConnected = state;
                if (state) _ = RefreshDataAsync();
            };

            await RefreshDataAsync();
            _ = LoadAllMembersAsync();
            _ = DiscordEvents.InitializeAsync();

            // Periodic fallback: catches any SignalR events missed during the startup window
            _periodicRefreshTimer = new System.Windows.Threading.DispatcherTimer(
                System.Windows.Threading.DispatcherPriority.Background,
                System.Windows.Application.Current.Dispatcher)
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _periodicRefreshTimer.Tick += (_, _) => _ = RefreshDataAsync();
            _periodicRefreshTimer.Start();
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

                var isModerator = PackTracker.Domain.Security.SecurityConstants.IsElevatedRequestRole(profile.DiscordRank);
                foreach (var window in OpenChatWindows)
                {
                    window.CurrentUsername = _currentUsername;
                    window.CurrentUserDisplayName = CurrentUserDisplayName;
                    window.IsCurrentUserModerator = isModerator;
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
            if (window == null && msg.Channel.StartsWith("dm:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = msg.Channel.Split(':');
                if (parts.Length == 3)
                {
                    var selfLower = _currentUsername?.Trim().ToLowerInvariant() ?? string.Empty;
                    var counterpart = parts[1].Equals(selfLower, StringComparison.OrdinalIgnoreCase)
                        ? parts[2]
                        : parts[1];
                    window = EnsureDirectMessageWindow(counterpart, msg.SenderDisplayName);
                }
            }

            if (window != null)
            {
                window.ReceiveMessage(
                    msg.Id,
                    msg.Sender,
                    msg.SenderDisplayName,
                    msg.Content,
                    msg.SentAt,
                    msg.SenderRole,
                    msg.AvatarUrl);
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
            var onlineSet = new HashSet<string>(
                users.Select(u => u.Username ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            // Refresh avatars for online users via the cache service
            var avatarMap = new Dictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);
            foreach (var u in users)
            {
                if (!string.IsNullOrWhiteSpace(u.AvatarUrl))
                {
                    var bmp = await _avatarCache.GetAvatarAsync(u.AvatarUrl);
                    if (bmp != null)
                        avatarMap[u.Username ?? string.Empty] = bmp;
                }
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var vm in AllMembers)
                {
                    vm.IsOnline = onlineSet.Contains(vm.Username ?? string.Empty);

                    // Apply fresher avatar for online users; offline users keep their cached image.
                    if (vm.IsOnline && avatarMap.TryGetValue(vm.Username ?? string.Empty, out var bmp))
                        vm.AvatarImage = bmp;
                }
            });
        });
    }

    public async Task RefreshDataAsync()
    {
        try
        {
            RequestLoadError = null;
            using var client = _apiClientProvider.CreateClient();
            using var response = await client.GetAsync("api/v1/dashboard/summary");
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // Try to extract just the message field from the error JSON
                string errorDetail;
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(body);
                    errorDetail = doc.RootElement.TryGetProperty("message", out var msg)
                        ? msg.GetString() ?? body
                        : body;
                }
                catch { errorDetail = body; }

                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    RequestLoadError = $"Server error ({(int)response.StatusCode}): {errorDetail}";
                }));
                return;
            }

            DashboardSummaryDto? summary;
            try
            {
                summary = System.Text.Json.JsonSerializer.Deserialize<DashboardSummaryDto>(
                    body,
                    DashboardJsonOptions);
            }
            catch (System.Text.Json.JsonException)
            {
                var snippet = body.Trim();
                if (snippet.Length > 160)
                    snippet = snippet[..160] + "...";

                var detail = response.Content.Headers.ContentType?.MediaType is { } mediaType
                    && mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                    ? "Received HTML instead of JSON. Authentication may have expired."
                    : $"Received an invalid response from the server: {snippet}";

                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    RequestLoadError = detail;
                }));
                return;
            }

            if (summary != null)
            {
                var personalContext = summary.PersonalContext ?? new PersonalContextDto();

                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    ActiveRequests.Clear();
                    PinnedRequests.Clear();
                    RegularRequests.Clear();

                    foreach (var req in summary.ActiveRequests)
                    {
                        var item = new ActiveRequestItemViewModel(req, DismissItem);

                        if (req.IsRequestedByCurrentUser)
                        {
                            if (_lastKnownClaimCounts.TryGetValue(req.Id, out var lastKnown))
                            {
                                var delta = req.ClaimCount - lastKnown;
                                if (delta > 0)
                                {
                                    _newClaimCounts.TryGetValue(req.Id, out var existing);
                                    _newClaimCounts[req.Id] = Math.Max(existing, delta);
                                    _lastKnownClaimCounts[req.Id] = req.ClaimCount;
                                }
                            }
                            else
                            {
                                // First time seeing this request this session.
                                // If it already has claims, badge it — the user hasn't seen this yet.
                                if (req.ClaimCount > 0)
                                {
                                    _newClaimCounts.TryGetValue(req.Id, out var existing);
                                    _newClaimCounts[req.Id] = Math.Max(existing, req.ClaimCount);
                                }
                                _lastKnownClaimCounts[req.Id] = req.ClaimCount;
                            }
                        }

                        if (_newClaimCounts.TryGetValue(req.Id, out var count) && count > 0)
                            item.SetNewClaimCount(count);

                        ActiveRequests.Add(item);
                        if (req.IsPinned)
                            PinnedRequests.Add(item);
                        else
                            RegularRequests.Add(item);
                    }

                    MyActiveTasks.Clear();
                    foreach (var req in personalContext.MyActiveTasks)
                    {
                        var item = new ActiveRequestItemViewModel(req, DismissItem);
                        if (_newClaimCounts.TryGetValue(req.Id, out var count) && count > 0)
                            item.SetNewClaimCount(count);
                        MyActiveTasks.Add(item);
                    }

                    MyOpenRequests.Clear();
                    foreach (var req in personalContext.MyPendingRequests)
                    {
                        var item = new ActiveRequestItemViewModel(req, DismissItem);
                        if (_newClaimCounts.TryGetValue(req.Id, out var count) && count > 0)
                            item.SetNewClaimCount(count);
                        MyOpenRequests.Add(item);
                    }

                    OnPropertyChanged(nameof(PinnedRequests));
                    OnPropertyChanged(nameof(RegularRequests));
                    RecalcNewClaimsTotal();
                }));

                await Guide.RefreshAsync();
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => OnPropertyChanged(nameof(TopGuideRequests))));
            }
        }
        catch (Exception ex)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                if (ex is HttpRequestException httpEx && httpEx.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    RequestLoadError = "Authentication expired. Please sign in again.";
                    return;
                }

                RequestLoadError = $"Failed to load requests: {ex.Message}";
            }));
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
    public ObservableCollection<OnlineUserViewModel> AllMembers { get; }
    public ObservableCollection<ActiveRequestItemViewModel> ActiveRequests { get; }
    public ObservableCollection<ActiveRequestItemViewModel> PinnedRequests { get; }
    public ObservableCollection<ActiveRequestItemViewModel> RegularRequests { get; }
    public ObservableCollection<ActiveRequestItemViewModel> MyActiveTasks { get; }
    public ObservableCollection<ActiveRequestItemViewModel> MyOpenRequests { get; }

    public string? RequestLoadError
    {
        get => _requestLoadError;
        private set => SetProperty(ref _requestLoadError, value);
    }

    public int TotalNewClaimsCount
    {
        get => _totalNewClaimsCount;
        private set
        {
            if (SetProperty(ref _totalNewClaimsCount, value))
                OnPropertyChanged(nameof(HasAnyNewClaims));
        }
    }

    public bool HasAnyNewClaims => _totalNewClaimsCount > 0;

    public ICommand DismissAllNewClaimsCommand { get; }

    public GuideDashboardViewModel Guide { get; }
    public DiscordEventsViewModel DiscordEvents { get; }
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

    private void OnClaimConfirmed(ClaimNotificationDto dto)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _newClaimCounts.TryGetValue(dto.RequestId, out var current);
            _newClaimCounts[dto.RequestId] = current + 1;

            foreach (var item in AllRequestItems().Where(x => x.Id == dto.RequestId))
                item.IncrementNewClaim();

            RecalcNewClaimsTotal();
        });
    }

    private void DismissItem(ActiveRequestItemViewModel dismissed)
    {
        _newClaimCounts.Remove(dismissed.Id);
        // Acknowledge the current claim count so the next refresh won't re-badge
        _lastKnownClaimCounts[dismissed.Id] = dismissed.ClaimCount;

        foreach (var item in AllRequestItems().Where(x => x.Id == dismissed.Id && x != dismissed))
            item.ClearNewClaims();

        RecalcNewClaimsTotal();
    }

    private void DismissAllNewClaims()
    {
        _newClaimCounts.Clear();
        foreach (var item in AllRequestItems())
        {
            _lastKnownClaimCounts[item.Id] = item.ClaimCount;
            item.ClearNewClaims();
        }
        RecalcNewClaimsTotal();
    }

    private void RecalcNewClaimsTotal()
    {
        TotalNewClaimsCount = _newClaimCounts.Values.Sum();
    }

    private IEnumerable<ActiveRequestItemViewModel> AllRequestItems()
        => ActiveRequests.Concat(MyActiveTasks).Concat(MyOpenRequests);

    private void LoadChatChannelsForRole(string? division)
    {
        // Preserve any DM channel entries that were dynamically added
        var existingDms = AvailableChatChannels.Where(c => c.IsDirectMessage).ToList();
        AvailableChatChannels.Clear();
        foreach (var dm in existingDms)
            AvailableChatChannels.Add(dm);

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

        // DM sidebar entries route through EnsureDirectMessageWindow
        if (channel.IsDirectMessage && !string.IsNullOrWhiteSpace(channel.TargetUsername))
        {
            foreach (var ch in AvailableChatChannels)
                ch.IsSelected = false;
            channel.IsSelected = true;
            channel.HasUnread = false;
            channel.UnreadCount = 0;

            var dmWindow = EnsureDirectMessageWindow(channel.TargetUsername, channel.DisplayName);
            ActiveChatWindow = dmWindow;
            return;
        }

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

        var window = new ChatWindowViewModel(CloseWindow, BringToFront, OnWindowStateChanged, _avatarCache)
        {
            ChannelKey = channel.Key,
            Title = channel.DisplayName,
            AccentBrush = channel.AccentBrush,
            CurrentUserDisplayName = CurrentUserDisplayName,
            CurrentUsername = _currentUsername,
            IsCurrentUserModerator = PackTracker.Domain.Security.SecurityConstants.IsElevatedRequestRole(_currentUserRole),
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

    private void OnPendingDirectMessages(IReadOnlyList<PendingDmDto> dms)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var dm in dms)
            {
                if (string.IsNullOrWhiteSpace(dm.LastSenderUsername)) continue;
                var window = EnsureDirectMessageWindow(dm.LastSenderUsername, dm.LastSenderDisplayName);
                window.UnreadCount += dm.UnreadCount;

                // Ensure sidebar entry reflects the new unread count immediately
                var sidebarEntry = AvailableChatChannels.FirstOrDefault(c => c.Key == window.ChannelKey);
                if (sidebarEntry != null)
                {
                    SyncUnreadState(window, sidebarEntry);
                }
            }
        });
    }

    private async Task LoadAllMembersAsync()
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();
            var profiles = await client.GetFromJsonAsync<List<MemberSummaryDto>>(
                "api/v1/profiles", DashboardJsonOptions) ?? new();

            var vms = profiles
                .Select(p => new OnlineUserViewModel
                {
                    Username = p.Username,
                    DiscordDisplayName = p.DiscordDisplayName,
                    ContactLabel = p.DiscordDisplayName ?? p.Username,
                    Role = p.DiscordRank,
                    RoleColorBrush = OnlineUserViewModel.GetRoleColor(p.DiscordRank),
                    IsOnline = _signalR.IsUserOnline(p.Username)
                })
                .OrderBy(v => v.ContactLabel, StringComparer.OrdinalIgnoreCase)
                .ToList();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AllMembers.Clear();
                foreach (var vm in vms)
                    AllMembers.Add(vm);
            });

            // Download Discord avatars for all members (solves race with OnPresenceUpdated)
            var urlMap = profiles
                .Where(p => !string.IsNullOrWhiteSpace(p.DiscordAvatarUrl))
                .ToDictionary(p => p.Username, p => p.DiscordAvatarUrl!, StringComparer.OrdinalIgnoreCase);

            if (urlMap.Count > 0)
                await DownloadAndApplyAvatarsAsync(urlMap);
        }
        catch { }
    }

    private async Task DownloadAndApplyAvatarsAsync(Dictionary<string, string> urlMap)
    {
        var bitmapMap = new Dictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);

        foreach (var (username, url) in urlMap)
        {
            var bmp = await _avatarCache.GetAvatarAsync(url);
            if (bmp != null)
                bitmapMap[username] = bmp;
        }

        if (bitmapMap.Count == 0)
            return;

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var vm in AllMembers)
            {
                if (bitmapMap.TryGetValue(vm.Username ?? string.Empty, out var bmp))
                    vm.AvatarImage = bmp;
            }
        });
    }

    private ChatWindowViewModel EnsureDirectMessageWindow(string username, string? displayName)
    {
        var channelKey = BuildDirectChannelKey(username);
        var label = displayName ?? username;

        // Add/update sidebar channel entry for this DM
        var sidebarEntry = AvailableChatChannels.FirstOrDefault(c => c.IsDirectMessage && c.TargetUsername == username);
        if (sidebarEntry == null)
        {
            sidebarEntry = new AvailableChannelViewModel
            {
                Key = channelKey,
                DisplayName = label,
                AccessSummary = "Direct message",
                AccentBrush = BrushFromHex("#6A4F8B"),
                IsDirectMessage = true,
                TargetUsername = username,
            };
            // Insert DM entries at the top of the list
            AvailableChatChannels.Insert(0, sidebarEntry);
        }

        var existing = OpenChatWindows.FirstOrDefault(x => x.ChannelKey == channelKey);
        if (existing != null)
            return existing;

        var window = new ChatWindowViewModel(CloseWindow, BringToFront, OnWindowStateChanged, _avatarCache)
        {
            ChannelKey = channelKey,
            Title = $"DM // {label}",
            TargetUsername = username,
            TargetDisplayName = label,
            CurrentUserDisplayName = CurrentUserDisplayName,
            CurrentUsername = _currentUsername,
            AccentBrush = BrushFromHex("#6A4F8B"),
            IsCurrentUserModerator = PackTracker.Domain.Security.SecurityConstants.IsElevatedRequestRole(_currentUserRole),
        };

        window.Messages.CollectionChanged += (_, _) => SyncUnreadState(window, sidebarEntry);
        window.MessageSent += (s, content) => { _ = SendWindowMessageAsync(window, content); };
        window.EditRequested += (ch, msgId, newContent) => { _ = _signalR.EditMessageAsync(ch, msgId, newContent); };
        window.DeleteRequested += (ch, msgId) => { _ = _signalR.DeleteMessageAsync(ch, msgId); };
        OpenChatWindows.Add(window);

        if (_signalR.IsConnected)
            _ = _signalR.GetDirectMessageHistoryAsync(username);

        return window;
    }

    private string BuildDirectChannelKey(string username)
    {
        var self = _currentUsername?.Trim().ToLowerInvariant() ?? string.Empty;
        var other = username.Trim().ToLowerInvariant();
        var parts = new[] { self, other };
        Array.Sort(parts);
        return $"dm:{parts[0]}:{parts[1]}";
    }

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

    private record MemberSummaryDto(
        string Username,
        string? DiscordDisplayName,
        string? DiscordRank,
        string? DiscordAvatarUrl);
}
