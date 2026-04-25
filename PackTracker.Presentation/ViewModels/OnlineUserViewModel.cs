using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PackTracker.Presentation.ViewModels;

public sealed class OnlineUserViewModel : INotifyPropertyChanged
{
    private ImageSource? _avatarImage;
    private string? _contactLabel;
    private string? _role;
    private Brush? _roleColorBrush;

    public string? Username { get; set; }

    public string? DiscordDisplayName { get; set; }

    private bool _isOnline = true;
    public bool IsOnline
    {
        get => _isOnline;
        set
        {
            if (_isOnline != value)
            {
                _isOnline = value;
                OnPropertyChanged();
            }
        }
    }

    public static Brush GetRoleColor(string? role)
    {
        return role switch
        {
            "LOCOPS" => BrushFromHex("#5C8B5E"),
            "TACOPS" => BrushFromHex("#A36E2F"),
            "SPECOPS" => BrushFromHex("#844F4F"),
            "ARCOPS" => BrushFromHex("#1A6E6E"),
            "Leadership" => BrushFromHex("#B090E0"),
            _ => BrushFromHex("#808080")
        };
    }

    private static Brush BrushFromHex(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
    
    public ImageSource? AvatarImage
    {
        get => _avatarImage;
        set
        {
            if (!Equals(_avatarImage, value))
            {
                _avatarImage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAvatar));
            }
        }
    }

    public bool HasAvatar => AvatarImage != null;

    public string? ContactLabel
    {
        get => _contactLabel;
        set
        {
            if (_contactLabel != value)
            {
                _contactLabel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(InitialLetter));
            }
        }
    }

    public string InitialLetter =>
        !string.IsNullOrWhiteSpace(ContactLabel)
            ? ContactLabel.Substring(0, 1).ToUpperInvariant()
            : "?";

    public string? Role
    {
        get => _role;
        set
        {
            if (_role != value)
            {
                _role = value;
                OnPropertyChanged();
            }
        }
    }

    public Brush? RoleColorBrush
    {
        get => _roleColorBrush;
        set
        {
            if (_roleColorBrush != value)
            {
                _roleColorBrush = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}