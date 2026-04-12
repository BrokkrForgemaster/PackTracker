using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Media;
using PackTracker.Presentation.Commands;

namespace PackTracker.Presentation.ViewModels;

public class ChatWindowViewModel : ViewModelBase
{
    private static readonly Regex DirectMentionPattern = new(
        @"^\s*@(?<username>[A-Za-z0-9_.-]+)\s+.+$",
        RegexOptions.Compiled);

    private readonly Action<ChatWindowViewModel> _closeAction;
    private readonly Action<ChatWindowViewModel> _bringToFrontAction;
    private readonly Action<ChatWindowViewModel> _expandedAction;

    public event Action<string, string>? MessageSent;

    private double _left;
    private double _top;
    private double _width = 380;
    private double _windowHeight = 420;
    private int _zIndex;
    private bool _isCollapsed;
    private bool _hasUnread;
    private int _unreadCount;
    private string _draftMessage = string.Empty;

    public ChatWindowViewModel(
        Action<ChatWindowViewModel> closeAction,
        Action<ChatWindowViewModel> bringToFrontAction,
        Action<ChatWindowViewModel> expandedAction)
    {
        _closeAction = closeAction;
        _bringToFrontAction = bringToFrontAction;
        _expandedAction = expandedAction;

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
            if (SetProperty(ref _draftMessage, value))
            {
                if (SendMessageCommand is RelayCommand cmd)
                    cmd.RaiseCanExecuteChanged();
            }
        }
    }

    public bool ShowUnreadBadge => IsCollapsed && HasUnread;

    public string AlertCaption => $"{Title} ({UnreadCount} new)";

    public ICommand SendMessageCommand { get; }
    public ICommand CollapseCommand { get; }
    public ICommand ExpandCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand PopFrontCommand { get; }

    public void ReceiveMessage(string sender, string content)
    {
        Messages.Add(new ChatMessageViewModel
        {
            SenderDisplayName = sender,
            Content = content,
            SentAt = DateTime.Now
        });

        if (IsCollapsed)
            UnreadCount++;
    }

    private bool CanSendMessage()
    {
        return !string.IsNullOrWhiteSpace(DraftMessage);
    }

    private void SendMessage()
    {
        var text = DraftMessage?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (!IsDirectMessage && !LooksLikeDirectMention(text))
        {
            Messages.Add(new ChatMessageViewModel
            {
                SenderDisplayName = "You",
                Content = text,
                SentAt = DateTime.Now
            });
        }

        DraftMessage = string.Empty;
        MessageSent?.Invoke(ChannelKey, text);
        _bringToFrontAction(this);
    }

    private static bool LooksLikeDirectMention(string text) =>
        DirectMentionPattern.IsMatch(text);

    private void ExpandWindow()
    {
        IsCollapsed = false;
        UnreadCount = 0;
        HasUnread = false;
        _bringToFrontAction(this);
        _expandedAction(this);
    }
}
