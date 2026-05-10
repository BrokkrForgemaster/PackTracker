using System.Collections.ObjectModel;
using PackTracker.Mobile.Services;

namespace PackTracker.Mobile.Pages;

/// <summary>
/// Display model wrapping a <see cref="ChatMessage"/> for the CollectionView.
/// </summary>
public sealed class ChatMessageViewModel
{
    // Colors used for sender labels — rotated by hash of username
    private static readonly Color[] SenderPalette =
    {
        Color.FromArgb("#D6A04B"), // AccentGold
        Color.FromArgb("#B8A88A"), // TextSecondary
        Color.FromArgb("#4F6A84"), // BlueBorder
        Color.FromArgb("#00A86B"), // GreenActive
        Color.FromArgb("#B15B34"), // AccentRedBorder
        Color.FromArgb("#B87E3A"), // AccentGoldMuted
    };

    public string Id            { get; }
    public string Channel       { get; }
    public string Sender        { get; }
    public string SenderDisplayName { get; }
    public string Content       { get; set; }
    public string TimeLabel     { get; }
    public Color  SenderColor   { get; }
    public bool   IsEdited      { get; set; }
    public string IsEditedLabel => IsEdited ? "(edited)" : string.Empty;

    public ChatMessageViewModel(ChatMessage msg, string? currentUsername)
    {
        Id              = msg.Id;
        Channel         = msg.Channel;
        Sender          = msg.Sender;
        SenderDisplayName = msg.SenderDisplayName;
        Content         = msg.Content;
        TimeLabel       = msg.SentAt.ToString("HH:mm");
        IsEdited        = false;

        // Own messages always get gold; others get a color based on username hash
        if (!string.IsNullOrWhiteSpace(currentUsername) &&
            string.Equals(msg.Sender, currentUsername, StringComparison.OrdinalIgnoreCase))
        {
            SenderColor = Color.FromArgb("#D6A04B"); // AccentGold
        }
        else
        {
            var hash = Math.Abs(msg.Sender.GetHashCode());
            SenderColor = SenderPalette[hash % SenderPalette.Length];
        }
    }
}

[QueryProperty(nameof(DmUsername),    "dm")]
[QueryProperty(nameof(DmDisplayName), "displayName")]
public partial class ChatPage : ContentPage
{
    private readonly MobileChatService     _chat;
    private readonly PackTrackerApiClient  _api;
    private readonly MobileSessionService  _session;

    private string  _currentChannel = "general";
    private bool    _eventsWired;
    private bool    _profileLoaded;

    // per-channel message collections
    private readonly Dictionary<string, ObservableCollection<ChatMessageViewModel>> _allMessages
        = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<string> _availableChannels = new();
    private readonly List<Button> _channelChips      = new();

    // Query parameters for DM navigation
    public string? DmUsername    { get; set; }
    public string? DmDisplayName { get; set; }

    public ChatPage(MobileChatService chatService, PackTrackerApiClient api, MobileSessionService session)
    {
        InitializeComponent();
        _chat    = chatService;
        _api     = api;
        _session = session;
    }

    // ──────────────────────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await InitializeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                DisplayAlert("Chat Error", ex.Message, "OK"));
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        UnwireEvents();
    }

    // ──────────────────────────────────────────────────────────────────
    // Initialization
    // ──────────────────────────────────────────────────────────────────

    private async Task InitializeAsync()
    {
        // Load profile only once per page lifetime
        if (!_profileLoaded)
        {
            await LoadProfileAsync().ConfigureAwait(false);
            _profileLoaded = true;
        }

        // Connect SignalR if needed
        if (!_chat.IsConnected)
            await _chat.ConnectAsync().ConfigureAwait(false);

        // Wire events once
        if (!_eventsWired)
        {
            _chat.MessageReceived        += OnMessageReceived;
            _chat.MessageEdited          += OnMessageEdited;
            _chat.MessageDeleted         += OnMessageDeleted;
            _chat.PresenceUpdated        += OnPresenceUpdated;
            _chat.ConnectionStateChanged += OnConnectionChanged;
            _eventsWired = true;
        }

        // Handle DM navigation parameter
        if (!string.IsNullOrWhiteSpace(DmUsername))
        {
            var dmKey = MobileChatService.BuildDmChannelKey(
                _chat.CurrentUsername ?? string.Empty, DmUsername);

            if (!_availableChannels.Contains(dmKey))
                _availableChannels.Add(dmKey);

            await MainThread.InvokeOnMainThreadAsync(() => BuildChannelBar());
            await SelectChannelAsync(dmKey).ConfigureAwait(false);

            // Load DM history
            await _chat.GetDirectMessageHistoryAsync(DmUsername).ConfigureAwait(false);

            // Reset so the same DM target isn't re-opened on every OnAppearing
            DmUsername    = null;
            DmDisplayName = null;
        }
        else
        {
            await SelectChannelAsync("general").ConfigureAwait(false);
        }
    }

    private async Task LoadProfileAsync()
    {
        var profile = await _api.GetAsync<ProfileSummary>("api/v1/profiles/me").ConfigureAwait(false);

        _chat.CurrentUsername = profile?.Username;
        var division = profile?.DiscordDivision ?? string.Empty;

        // Build available channels from division membership
        _availableChannels.Clear();
        _availableChannels.Add("general");

        if (division.Equals("LOCOPS",     StringComparison.OrdinalIgnoreCase) ||
            division.Equals("Leadership", StringComparison.OrdinalIgnoreCase))
            _availableChannels.Add("locops");

        if (division.Equals("TACOPS",     StringComparison.OrdinalIgnoreCase) ||
            division.Equals("Leadership", StringComparison.OrdinalIgnoreCase))
            _availableChannels.Add("tacops");

        if (division.Equals("SPECOPS",    StringComparison.OrdinalIgnoreCase) ||
            division.Equals("Leadership", StringComparison.OrdinalIgnoreCase))
            _availableChannels.Add("specops");

        if (division.Equals("ARCOPS",     StringComparison.OrdinalIgnoreCase) ||
            division.Equals("Leadership", StringComparison.OrdinalIgnoreCase))
            _availableChannels.Add("arcops");

        if (division.Equals("Leadership", StringComparison.OrdinalIgnoreCase))
            _availableChannels.Add("leadership");

        await MainThread.InvokeOnMainThreadAsync(() => BuildChannelBar());
    }

    // ──────────────────────────────────────────────────────────────────
    // Channel bar
    // ──────────────────────────────────────────────────────────────────

    private void BuildChannelBar()
    {
        ChannelBar.Children.Clear();
        _channelChips.Clear();

        foreach (var channel in _availableChannels)
        {
            var label   = IsDmChannel(channel) ? DmChipLabel(channel) : channel.ToUpperInvariant();
            var isActive = string.Equals(channel, _currentChannel, StringComparison.OrdinalIgnoreCase);

            var chip = new Button
            {
                Text         = label,
                CornerRadius = 12,
                Padding      = new Thickness(12, 6),
                FontSize     = 12,
                FontAttributes = FontAttributes.Bold,
                BorderWidth  = 1,
            };
            ApplyChipStyle(chip, isActive);

            var captured = channel;
            chip.Clicked += async (_, _) => await SelectChannelAsync(captured).ConfigureAwait(false);

            _channelChips.Add(chip);
            ChannelBar.Children.Add(chip);
        }
    }

    private void RefreshChipStyles()
    {
        for (int i = 0; i < _channelChips.Count && i < _availableChannels.Count; i++)
        {
            var isActive = string.Equals(
                _availableChannels[i], _currentChannel, StringComparison.OrdinalIgnoreCase);
            ApplyChipStyle(_channelChips[i], isActive);
        }
    }

    private static void ApplyChipStyle(Button chip, bool active)
    {
        if (active)
        {
            chip.BackgroundColor = Color.FromArgb("#D6A04B"); // AccentGold
            chip.TextColor       = Color.FromArgb("#0E0B08"); // MainBackground
            chip.BorderColor     = Color.FromArgb("#B87E3A"); // AccentGoldMuted
        }
        else
        {
            chip.BackgroundColor = Color.FromArgb("#1F1712"); // SurfaceBackground
            chip.TextColor       = Color.FromArgb("#B8A88A"); // TextSecondary
            chip.BorderColor     = Color.FromArgb("#38271D"); // BorderColor
        }
    }

    private static bool IsDmChannel(string channel) =>
        channel.StartsWith("dm:", StringComparison.OrdinalIgnoreCase);

    private string DmChipLabel(string dmKey)
    {
        var counterpart = MobileChatService.TryGetDmCounterpart(dmKey, _chat.CurrentUsername ?? string.Empty);
        return counterpart is not null ? $"DM:{counterpart.ToUpperInvariant()}" : dmKey.ToUpperInvariant();
    }

    // ──────────────────────────────────────────────────────────────────
    // Channel selection
    // ──────────────────────────────────────────────────────────────────

    private async Task SelectChannelAsync(string channel)
    {
        _currentChannel = channel;

        if (!_allMessages.ContainsKey(channel))
        {
            _allMessages[channel] = new ObservableCollection<ChatMessageViewModel>();

            // For regular channels, join and load history via SignalR
            if (!IsDmChannel(channel))
                await _chat.JoinChannelAsync(channel).ConfigureAwait(false);
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            RefreshChipStyles();
            MessageList.ItemsSource = _allMessages[channel];
        });
    }

    // ──────────────────────────────────────────────────────────────────
    // SignalR event handlers
    // ──────────────────────────────────────────────────────────────────

    private void OnMessageReceived(ChatMessage msg)
    {
        var vm = new ChatMessageViewModel(msg, _chat.CurrentUsername);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_allMessages.TryGetValue(msg.Channel, out var collection))
            {
                collection = new ObservableCollection<ChatMessageViewModel>();
                _allMessages[msg.Channel] = collection;

                // If this is a new DM channel we haven't seen, add it to the channel bar
                if (IsDmChannel(msg.Channel) && !_availableChannels.Contains(msg.Channel))
                {
                    _availableChannels.Add(msg.Channel);
                    BuildChannelBar();
                }
            }

            collection.Add(vm);

            // Auto-scroll to the latest message if this is the active channel
            if (string.Equals(msg.Channel, _currentChannel, StringComparison.OrdinalIgnoreCase) &&
                collection.Count > 0)
            {
                MessageList.ScrollTo(collection[^1], ScrollToPosition.End, animate: false);
            }
        });
    }

    private void OnMessageEdited(string channel, string msgId, string newContent)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_allMessages.TryGetValue(channel, out var collection))
                return;

            var vm = collection.FirstOrDefault(m => m.Id == msgId);
            if (vm is null)
                return;

            vm.Content  = newContent;
            vm.IsEdited = true;

            // Force CollectionView refresh by replacing the item
            var idx = collection.IndexOf(vm);
            if (idx >= 0)
            {
                collection.RemoveAt(idx);
                collection.Insert(idx, vm);
            }
        });
    }

    private void OnMessageDeleted(string channel, string msgId)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_allMessages.TryGetValue(channel, out var collection))
                return;

            var vm = collection.FirstOrDefault(m => m.Id == msgId);
            if (vm is not null)
                collection.Remove(vm);
        });
    }

    private void OnPresenceUpdated(IReadOnlyList<OnlineMember> members)
    {
        // Presence data is displayed on MembersPage; nothing to update here
    }

    private void OnConnectionChanged(bool connected)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ConnectionBanner.IsVisible = !connected;
        });
    }

    // ──────────────────────────────────────────────────────────────────
    // UI event handlers
    // ──────────────────────────────────────────────────────────────────

    private async void SendButton_Clicked(object? sender, EventArgs e)
    {
        var text = MessageEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        MessageEntry.Text = string.Empty;

        try
        {
            if (IsDmChannel(_currentChannel))
            {
                var counterpart = MobileChatService.TryGetDmCounterpart(
                    _currentChannel, _chat.CurrentUsername ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(counterpart))
                    await _chat.SendDirectMessageAsync(counterpart, text).ConfigureAwait(false);
            }
            else
            {
                await _chat.SendMessageAsync(_currentChannel, text).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                DisplayAlert("Send Failed", ex.Message, "OK"));
        }
    }

    private async void MembersButton_Clicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Members").ConfigureAwait(false);
    }

    // ──────────────────────────────────────────────────────────────────
    // Cleanup
    // ──────────────────────────────────────────────────────────────────

    private void UnwireEvents()
    {
        if (!_eventsWired)
            return;

        _chat.MessageReceived        -= OnMessageReceived;
        _chat.MessageEdited          -= OnMessageEdited;
        _chat.MessageDeleted         -= OnMessageDeleted;
        _chat.PresenceUpdated        -= OnPresenceUpdated;
        _chat.ConnectionStateChanged -= OnConnectionChanged;
        _eventsWired = false;
    }

    // ──────────────────────────────────────────────────────────────────
    // Profile DTO (local, avoids referencing Application layer models)
    // ──────────────────────────────────────────────────────────────────

    private sealed record ProfileSummary(
        string  Username,
        string? DiscordDisplayName,
        string? DiscordDivision);
}
