namespace PackTracker.Presentation.Services.Navigation;

public sealed class NavigationStateService
{
    public string? LastMainViewKey { get; private set; }
    public string? LastAdminViewKey { get; private set; }
    public object? LastMainViewState { get; private set; }

    public void CaptureMainView(string viewKey, object? state = null)
    {
        LastMainViewKey = viewKey;
        LastMainViewState = state;
    }

    public void CaptureAdminView(string viewKey)
    {
        LastAdminViewKey = viewKey;
    }
}
