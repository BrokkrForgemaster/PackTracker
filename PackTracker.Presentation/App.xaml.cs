using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Services;
using PackTracker.Presentation.Services;
using PackTracker.Presentation.Views;
using Serilog;

namespace PackTracker.Presentation;

/// <summary name="App">
/// Application entry point. Handles DI setup, logging, and view navigation.
/// </summary>
public partial class App
{
    #region Fields

    private IServiceProvider? _serviceProvider;

    #endregion

    #region Constructor

    public App()
    {
        // DO NOT attach Startup handler here if you have Startup="OnStartup" in App.xaml!
        // Startup += OnStartup;
    }

    #endregion

    #region Dependency Injection

    public static IServiceProvider BootstrapServices(StartupEventArgs? e = null)
    {
        var services = new ServiceCollection();

        // Make Serilog early (reads user-secrets + env)
        var cfg = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddUserSecrets<App>(optional: true)
            .AddEnvironmentVariables() // PACKTRACKER__XYZ etc.
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(cfg)
            .Enrich.FromLogContext()
            .CreateLogger();

        services.AddLogging(b => b.AddSerilog());

        // Expose the WPF Application to DI for services that need it (e.g., ThemeManager)
        services.AddSingleton<System.Windows.Application>(_ => System.Windows.Application.Current);

        // SettingsService needs a string path => use a factory
        services.AddSingleton<ISettingsService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SettingsService>>();
            var defaultConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            return new SettingsService(logger, defaultConfigPath);
        });

        // Resolve connection string with precedence: ENV -> user settings -> appsettings
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var settings = sp.GetRequiredService<ISettingsService>().GetSettings();

            // ENV can be named however you prefer; this is just an example
            var fromEnv = Environment.GetEnvironmentVariable("PACKTRACKER__CONNECTIONSTRING")
                          ?? cfg.GetConnectionString("DefaultConnection");

            var connectionString =
                !string.IsNullOrWhiteSpace(fromEnv) ? fromEnv :
                !string.IsNullOrWhiteSpace(settings.ConnectionString) ? settings.ConnectionString :
                throw new InvalidOperationException(
                    "Database connection string not found. Set PACKTRACKER__CONNECTIONSTRING env var or configure it in user settings.");

            options.UseNpgsql(connectionString);
        });

        // Core app services
        services.AddSingleton<IGameLogService, GameLogService>();
        services.AddSingleton<IKillEventService, KillEventService>();
        services.AddSingleton<IThemeManager, ThemeManager>();

        // Views & MainWindow
        services.AddTransient<SettingsView>();
        services.AddTransient<DashboardView>();
        services.AddTransient<WelcomeView>();
        services.AddSingleton<KillTracker>();
        services.AddSingleton<MainWindow>();

        // Let other parts resolve the App instance if they need to
        services.AddSingleton(Current);

        // HttpClient for anything that needs it
        services.AddHttpClient();

        return services.BuildServiceProvider();
    }

    #endregion

    #region Startup Logic

    private void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            _serviceProvider = BootstrapServices(e);

            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            var themeManager = _serviceProvider.GetRequiredService<IThemeManager>();
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

            // Load persisted settings
            var settings = settingsService.GetSettings();

            // Apply theme before showing any view
            if (!string.IsNullOrWhiteSpace(settings.Theme))
                themeManager.ApplyTheme(settings.Theme);

            // Go directly to dashboard if first run is complete
            if (settings.FirstRunComplete)
            {
                var dashboardView = _serviceProvider.GetRequiredService<DashboardView>();
                mainWindow.ContentFrame.Navigate(dashboardView);
            }
            else
            {
                var welcomeView = _serviceProvider.GetRequiredService<WelcomeView>();
                mainWindow.ContentFrame.Navigate(welcomeView);
            }

            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error during PackTracker startup.");
            MessageBox.Show(
                "A critical error occurred during application startup:\n\n" + ex.Message,
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error
            );
            Shutdown(-1);
        }
    }

    #endregion
}