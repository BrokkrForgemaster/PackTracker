using Microsoft.Extensions.Logging;
using PackTracker.SharedPresentation.Responsive;

namespace PackTracker.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<IResponsiveLayoutService, ResponsiveLayoutService>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<Pages.LoginPage>();
        builder.Services.AddTransient<Pages.DashboardPage>();
        builder.Services.AddTransient<Pages.RequestHubPage>();
        builder.Services.AddTransient<Pages.TradingHubPage>();
        builder.Services.AddTransient<Pages.BlueprintsPage>();
        builder.Services.AddTransient<Pages.CraftingQueuePage>();
        builder.Services.AddTransient<Pages.ProcurementQueuePage>();
        builder.Services.AddTransient<Pages.ProfilePage>();
        builder.Services.AddTransient<Pages.SettingsPage>();
        builder.Services.AddTransient<Pages.AdminPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
