namespace PackTracker.Presentation.ViewModels.Admin;

public sealed class AdminShellViewModel : ViewModelBase
{
    private string _adminTierLabel = "Admin";
    private string _currentSection = "Dashboard";
    private bool _canManageRoles;

    public string Title => "PackTracker Admin";

    public string AdminTierLabel
    {
        get => _adminTierLabel;
        set => SetProperty(ref _adminTierLabel, value);
    }

    public string CurrentSection
    {
        get => _currentSection;
        set => SetProperty(ref _currentSection, value);
    }

    public bool CanManageRoles
    {
        get => _canManageRoles;
        set => SetProperty(ref _canManageRoles, value);
    }
}
