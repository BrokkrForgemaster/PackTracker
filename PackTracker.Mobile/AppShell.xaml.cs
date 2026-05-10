namespace PackTracker.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(Pages.LoginPage), typeof(Pages.LoginPage));
        Routing.RegisterRoute(nameof(Pages.DashboardPage), typeof(Pages.DashboardPage));
        Routing.RegisterRoute(nameof(Pages.ChatPage), typeof(Pages.ChatPage));
        Routing.RegisterRoute(nameof(Pages.MembersPage), typeof(Pages.MembersPage));
        Routing.RegisterRoute(nameof(Pages.TradingHubPage), typeof(Pages.TradingHubPage));
        Routing.RegisterRoute(nameof(Pages.BlueprintsPage), typeof(Pages.BlueprintsPage));
        Routing.RegisterRoute(nameof(Pages.CraftingQueuePage), typeof(Pages.CraftingQueuePage));
        Routing.RegisterRoute(nameof(Pages.ProcurementQueuePage), typeof(Pages.ProcurementQueuePage));
        Routing.RegisterRoute(nameof(Pages.RequestHubPage), typeof(Pages.RequestHubPage));
        Routing.RegisterRoute(nameof(Pages.ProfilePage), typeof(Pages.ProfilePage));
        Routing.RegisterRoute(nameof(Pages.SettingsPage), typeof(Pages.SettingsPage));
        Routing.RegisterRoute(nameof(Pages.AdminPage), typeof(Pages.AdminPage));
    }
}
