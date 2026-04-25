using System.Globalization;
using System.Windows.Input;
using PackTracker.Presentation.Commands;

namespace PackTracker.Presentation.ViewModels;
public class ChatMessageViewModel : ViewModelBase
{
    private string _content = string.Empty;
    private bool _isEditing;
    private string _editDraft = string.Empty;
    private bool _isEdited;

    public string Id { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = string.Empty;
    public string? SenderRole { get; set; }
    public DateTime SentAt { get; set; }

    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    private System.Windows.Media.ImageSource? _avatarImage;

    public System.Windows.Media.ImageSource? AvatarImage
    {
        get => _avatarImage;
        set
        {
            SetProperty(ref _avatarImage, value);
            OnPropertyChanged(nameof(HasAvatar));
        }
    }

    public bool HasAvatar => AvatarImage != null;

    public bool IsOwnMessage { get; set; }
    public string? AvatarUrl { get; set; }

    // True when this user owns the message OR is a moderator — controls whether the ⋯ menu is shown.
    public bool CanDelete => DeleteCommand != null;

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public string EditDraft
    {
        get => _editDraft;
        set => SetProperty(ref _editDraft, value);
    }

    public bool IsEdited
    {
        get => _isEdited;
        set
        {
            if (SetProperty(ref _isEdited, value))
                OnPropertyChanged(nameof(FormattedTime));
        }
    }

    public string InitialLetter =>
        !string.IsNullOrEmpty(SenderDisplayName)
            ? SenderDisplayName[0].ToString().ToUpperInvariant()
            : "?";

    public string FormattedTime =>
        SentAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) + (IsEdited ? " (edited)" : "");

    public string RoleDisplay =>
        !string.IsNullOrWhiteSpace(SenderRole) ? SenderRole : string.Empty;

    // Commands wired by ChatWindowViewModel
    public ICommand? BeginEditCommand { get; set; }
    public ICommand? ConfirmEditCommand { get; set; }
    public ICommand? CancelEditCommand { get; set; }
    public ICommand? DeleteCommand { get; set; }
}