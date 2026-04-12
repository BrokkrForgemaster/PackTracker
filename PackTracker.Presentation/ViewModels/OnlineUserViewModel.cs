using System.Windows.Media;

namespace PackTracker.Presentation.ViewModels;

public class OnlineUserViewModel : ViewModelBase
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Role { get; set; }
    public Brush RoleColorBrush { get; set; } = Brushes.Gray;

    public string ContactLabel => string.IsNullOrWhiteSpace(Username)
        ? DisplayName
        : $"{DisplayName} (@{Username})";

    public string InitialLetter => string.IsNullOrWhiteSpace(DisplayName)
        ? "?"
        : DisplayName.Substring(0, 1).ToUpperInvariant();
}
