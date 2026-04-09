namespace PackTracker.Presentation.ViewModels;
public class ChatMessageViewModel : ViewModelBase
{
    public string Id { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string Content { get; set; } = string.Empty;
    
    public bool IsOwnMessage { get; set; }
    public string? AvatarUrl { get; set; }

    public string InitialLetter =>
        !string.IsNullOrEmpty(SenderDisplayName)
            ? SenderDisplayName[0].ToString().ToUpper()
            : "?";

    public string FormattedTime => SentAt.ToLocalTime().ToString("HH:mm");
}