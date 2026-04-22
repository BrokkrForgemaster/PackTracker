using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using PackTracker.Presentation.Commands;
using PackTracker.Presentation.Services;

namespace PackTracker.Presentation.ViewModels;

public class ChatWindowViewModel : ViewModelBase
{
    private static readonly Regex DirectMentionPattern = new(
        @"^\s*@(?<username>[A-Za-z0-9_.-]+)\s+.+$",
        RegexOptions.Compiled);

    private readonly Action<ChatWindowViewModel> _closeAction;
    private readonly Action<ChatWindowViewModel> _bringToFrontAction;
    private readonly Action<ChatWindowViewModel> _stateChangedAction;
    private readonly AvatarCacheService? _avatarCache;

    public event Action<string, string>? MessageSent;
    public event Action<string, string, string>? EditRequested;
    public event Action<string, string>? DeleteRequested;

    private double _left;
    private double _top;
    private double _width = 380;
    private double _windowHeight = 420;
    private int _zIndex;
    private bool _isCollapsed;
    private bool _hasUnread;
    private int _unreadCount;
    private string _draftMessage = string.Empty;
    private string? _currentUserDisplayName;
    private string? _currentUsername;
    private bool _isCurrentUserModerator;

    public ChatWindowViewModel(
        Action<ChatWindowViewModel> closeAction,
        Action<ChatWindowViewModel> bringToFrontAction,
        Action<ChatWindowViewModel> stateChangedAction,
        AvatarCacheService? avatarCache = null)
    {
        _closeAction = closeAction;
        _bringToFrontAction = bringToFrontAction;
        _stateChangedAction = stateChangedAction;
        _avatarCache = avatarCache;

        Messages = new ObservableCollection<ChatMessageViewModel>();

        SendMessageCommand = new RelayCommand(SendMessage, CanSendMessage);
        CollapseCommand = new RelayCommand(() => IsCollapsed = true);
        ExpandCommand = new RelayCommand(ExpandWindow);
        CloseCommand = new RelayCommand(() => _closeAction(this));
        PopFrontCommand = new RelayCommand(() => _bringToFrontAction(this));
    }

    public string ChannelKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? TargetUsername { get; set; }
    public string? TargetDisplayName { get; set; }
    public Brush AccentBrush { get; set; } = Brushes.Gray;
    public bool IsDirectMessage => !string.IsNullOrWhiteSpace(TargetUsername);

    public string? CurrentUserDisplayName
    {
        get => _currentUserDisplayName;
        set => SetProperty(ref _currentUserDisplayName, value);
    }

    public string? CurrentUsername
    {
        get => _currentUsername;
        set => SetProperty(ref _currentUsername, value);
    }

    public bool IsCurrentUserModerator
    {
        get => _isCurrentUserModerator;
        set => SetProperty(ref _isCurrentUserModerator, value);
    }

    public ObservableCollection<ChatMessageViewModel> Messages { get; }

    public double Left
    {
        get => _left;
        set => SetProperty(ref _left, value);
    }

    public double Top
    {
        get => _top;
        set => SetProperty(ref _top, value);
    }

    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }

    public double WindowHeight
    {
        get => _windowHeight;
        set => SetProperty(ref _windowHeight, value);
    }

    public int ZIndex
    {
        get => _zIndex;
        set => SetProperty(ref _zIndex, value);
    }

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (SetProperty(ref _isCollapsed, value))
            {
                OnPropertyChanged(nameof(ShowUnreadBadge));
                OnPropertyChanged(nameof(AlertCaption));
                _stateChangedAction(this);
            }
        }
    }

    public bool HasUnread
    {
        get => _hasUnread;
        set
        {
            if (SetProperty(ref _hasUnread, value))
            {
                OnPropertyChanged(nameof(ShowUnreadBadge));
                OnPropertyChanged(nameof(AlertCaption));
            }
        }
    }

    public int UnreadCount
    {
        get => _unreadCount;
        set
        {
            if (SetProperty(ref _unreadCount, value))
            {
                HasUnread = value > 0;
                OnPropertyChanged(nameof(ShowUnreadBadge));
                OnPropertyChanged(nameof(AlertCaption));
            }
        }
    }

    public string DraftMessage
    {
        get => _draftMessage;
        set
        {
            if (SetProperty(ref _draftMessage, value) && SendMessageCommand is RelayCommand cmd)
                cmd.RaiseCanExecuteChanged();
        }
    }

    public bool ShowUnreadBadge => IsCollapsed && HasUnread;

    public string AlertCaption => $"{Title} ({UnreadCount} new)";

    public ICommand SendMessageCommand { get; }
    public ICommand CollapseCommand { get; }
    public ICommand ExpandCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand PopFrontCommand { get; }

    public void ReceiveMessage(
        string id,
        string sender,
        string senderDisplayName,
        string content,
        DateTime sentAt,
        string? senderRole = null,
        string? avatarUrl = null)
    {
        if (!string.IsNullOrEmpty(id) && Messages.Any(m => m.Id == id))
            return;

        var isOwn = IsCurrentUser(sender, senderDisplayName);
        Messages.Add(CreateMessageVm(id, sender, senderDisplayName, content, sentAt, senderRole, isOwn, avatarUrl));

        if (IsCollapsed)
            UnreadCount++;
    }

    public void ApplyEdit(string messageId, string newContent)
    {
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg != null)
        {
            msg.Content = newContent;
            msg.IsEdited = true;
        }
    }

    public void ApplyDelete(string messageId)
    {
        var msg = Messages.FirstOrDefault(m => m.Id == messageId);
        if (msg != null)
            Messages.Remove(msg);
    }

    private bool CanSendMessage() => !string.IsNullOrWhiteSpace(DraftMessage);

    private void SendMessage()
    {
        var text = DraftMessage?.TrimEnd();
        if (string.IsNullOrWhiteSpace(text))
            return;

        DraftMessage = string.Empty;
        MessageSent?.Invoke(ChannelKey, text);
        _bringToFrontAction(this);
    }

    private ChatMessageViewModel CreateMessageVm(
        string id,
        string sender,
        string senderDisplayName,
        string content,
        DateTime sentAt,
        string? role,
        bool isOwn,
        string? avatarUrl = null)
    {
        var vm = new ChatMessageViewModel
        {
            Id = id,
            Sender = sender,
            SenderDisplayName = senderDisplayName,
            SenderRole = role,
            Content = content,
            SentAt = sentAt,
            IsOwnMessage = isOwn,
            AvatarUrl = avatarUrl
        };

        if (!string.IsNullOrWhiteSpace(avatarUrl) && _avatarCache != null)
        {
            _ = Task.Run(async () =>
            {
                var img = await _avatarCache.GetAvatarAsync(vm.AvatarUrl);
                if (img != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        vm.AvatarImage = img;
                    });
                }
            });
        }

        if (isOwn)
        {
            vm.BeginEditCommand = new RelayCommand(() =>
            {
                vm.EditDraft = vm.Content;
                vm.IsEditing = true;
            });
            vm.ConfirmEditCommand = new RelayCommand(() =>
            {
                var trimmed = vm.EditDraft?.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) && trimmed != vm.Content)
                {
                    EditRequested?.Invoke(ChannelKey, vm.Id, trimmed);
                    vm.Content = trimmed;
                    vm.IsEdited = true;
                }

                vm.IsEditing = false;
            });
            vm.CancelEditCommand = new RelayCommand(() => vm.IsEditing = false);
            vm.DeleteCommand = new RelayCommand(() => DeleteRequested?.Invoke(ChannelKey, vm.Id));
        }
        else if (_isCurrentUserModerator)
        {
            // Moderators can delete (but not edit) other users' messages.
            vm.DeleteCommand = new RelayCommand(() => DeleteRequested?.Invoke(ChannelKey, vm.Id));
        }

        return vm;
    }

    private static bool LooksLikeDirectMention(string text) =>
        DirectMentionPattern.IsMatch(text);

    private bool IsCurrentUser(string sender, string senderDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(CurrentUsername)
            && string.Equals(CurrentUsername, sender, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(CurrentUserDisplayName)
               && string.Equals(CurrentUserDisplayName, senderDisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private void ExpandWindow()
    {
        IsCollapsed = false;
        UnreadCount = 0;
        HasUnread = false;
        _bringToFrontAction(this);
    }
}
