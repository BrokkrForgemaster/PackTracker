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

    public bool IsOwnMessage { get; set; }
    public string? AvatarUrl { get; set; }

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
            ? SenderDisplayName[0].ToString().ToUpper()
            : "?";

    public string FormattedTime =>
        SentAt.ToLocalTime().ToString("HH:mm") + (IsEdited ? " (edited)" : "");

    public string RoleDisplay =>
        !string.IsNullOrWhiteSpace(SenderRole) ? SenderRole : string.Empty;

    // Commands wired by ChatWindowViewModel
    public ICommand? BeginEditCommand { get; set; }
    public ICommand? ConfirmEditCommand { get; set; }
    public ICommand? CancelEditCommand { get; set; }
    public ICommand? DeleteCommand { get; set; }
}