using System.Windows.Media;
using PackTracker.Domain.Security;

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

    /// <summary>
    /// Returns a color appropriate for the user's rank.
    /// </summary>
    public static Brush GetRoleColor(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return BrushFromHex("#B89C78");

        if (SecurityConstants.IsElevatedRequestRole(role))
            return BrushFromHex("#D4AF37"); // gold for leadership

        var pos = SecurityConstants.GetRolePosition(role);
        return pos switch
        {
            >= 6 => BrushFromHex("#D4AF37"),  // Captain+ gold
            >= 3 => BrushFromHex("#C0C0C0"),  // Platoon Sergeant+ silver
            >= 0 => BrushFromHex("#CD7F32"),   // Foundling+ bronze
            _ => BrushFromHex("#B89C78")       // unknown
        };
    }

    private static Brush BrushFromHex(string hex)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
    }
}
