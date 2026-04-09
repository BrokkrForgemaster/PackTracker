using System.Windows.Media;

namespace PackTracker.Presentation.ViewModels;

public class AvailableChannelViewModel : ViewModelBase
{
    private bool _hasUnread;
    private int _unreadCount;

    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AccessSummary { get; set; } = string.Empty;
    public Brush AccentBrush { get; set; } = Brushes.Gray;

    public bool HasUnread
    {
        get => _hasUnread;
        set => SetProperty(ref _hasUnread, value);
    }

    public int UnreadCount
    {
        get => _unreadCount;
        set => SetProperty(ref _unreadCount, value);
    }
}