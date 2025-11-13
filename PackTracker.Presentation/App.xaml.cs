using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;
using PackTracker.Infrastructure;
using PackTracker.Infrastructure.Services;
using PackTracker.Presentation.Services;
using PackTracker.Presentation.ViewModels;
using PackTracker.Presentation.Views;
using Serilog;

namespace PackTracker.Presentation;

/// <summary>
/// Main application entry point. Handles dependency injection, logging, and navigation.
/// </summary>
public partial class App : System.Windows.Application
{
    private IServiceProvider? _serviceProvider;
    public bool IsServiceProviderReady => _serviceProvider is not null;

    public static T GetService<T>() where T : notnull
    {
        if (Current is not App app || app._serviceProvider is null)
            throw new InvalidOperationException("❌ Service provider not initialized.");
        return app._serviceProvider.GetRequiredService<T>();
    }

    // -------------------------------------------------------------
    // 🧩 Central Bootstrap Method
    // -------------------------------------------------------------
    private static IServiceProvider BootstrapServices(IConfiguration cfg)
    {
        var services = new ServiceCollection();

        // 1️⃣ Logging configuration
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(cfg)
            .Enrich.FromLogContext()
            .WriteTo.File("Logs/PackTracker.log", rollingInterval: RollingInterval.Day)
            .WriteTo.Console()
            .CreateLogger();

        services.AddLogging(b => b.AddSerilog());

        // 2️⃣ Settings service (merge environment, secrets, local file)
        var settingsLoggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());
        var settingsLogger = settingsLoggerFactory.CreateLogger<SettingsService>();
        var settingsService = new SettingsService(settingsLogger);
        settingsService.EnsureBootstrapDefaults(cfg);
        services.AddSingleton<ISettingsService>(settingsService);

        // 3️⃣ Infrastructure (requires settings)
        services.AddInfrastructure(settingsService);

        // 4️⃣ Application + hosted API
        services.AddSingleton<System.Windows.Application>(_ => Current);
        services.AddSingleton<ApiHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<ApiHostedService>());

        // 5️⃣ External API configs
        services.Configure<RegolithOptions>(cfg.GetSection("Regolith"));
        services.AddHttpClient<IRegolithService, RegolithService>();
        services.Configure<UexOptions>(cfg.GetSection("Uex"));
        services.AddHttpClient<IUexService, UexService>();

        // 6️⃣ Core presentation services
        services.AddSingleton<IThemeManager, ThemeManager>();
        services.AddSingleton<IApiClientProvider, ApiClientProvider>();

        // 7️⃣ Views + ViewModels
        services.AddSingleton<MainWindow>();
        services.AddSingleton<WelcomeView>();
        services.AddSingleton<LoginView>();
        services.AddSingleton<DashboardView>();
        services.AddTransient<RequestsView>();
        services.AddTransient<RequestsViewModel>();
        services.AddSingleton<KillTracker>();
        services.AddSingleton<RefineryJobsView>();
        services.AddTransient<RefineryJobsViewModel>();
        services.AddTransient<UexView>();
        services.AddTransient<UexViewModel>(sp =>
            new UexViewModel(
                sp.GetRequiredService<IUexService>(),
                sp.GetRequiredService<ILogger<UexViewModel>>()));
        // Bind GuideRequest and Api options from configuration
        services.Configure<GuideRequestOptions>(cfg.GetSection(GuideRequestOptions.SectionName));

        services.AddSingleton<GuideNotificationService>();
        services.AddSingleton<GuideRequestWatcher>();
        services.AddSingleton<GuideAssignmentHandler>();
        services.AddTransient<GuideDashboardViewModel>();
        services.AddTransient<NewRequestViewModel>();
        services.AddTransient<SettingsView>();
        services.AddSignalR();
        

        return services.BuildServiceProvider();
    }

    // -------------------------------------------------------------
    // 🚀 Application Startup
    // -------------------------------------------------------------
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Load configuration
            var cfg = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddUserSecrets<App>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            // Bootstrap DI
            _serviceProvider = BootstrapServices(cfg);

            var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            logger.LogInformation("🚀 Starting PackTracker...");

            // Start embedded API host
            var apiHost = _serviceProvider.GetRequiredService<ApiHostedService>();
            await apiHost.StartAsync(CancellationToken.None);

            // Load user settings
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            var settings = settingsService.GetSettings();

            // Theme
            var themeManager = _serviceProvider.GetRequiredService<IThemeManager>();
            if (!string.IsNullOrWhiteSpace(settings.Theme))
                themeManager.ApplyTheme(settings.Theme);

            // Setup main window
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

            if (settings.FirstRunComplete)
                mainWindow.ContentFrame.Navigate(_serviceProvider.GetRequiredService<LoginView>());
            else
                mainWindow.ContentFrame.Navigate(_serviceProvider.GetRequiredService<WelcomeView>());

            mainWindow.Show();
            Log.Information("✅ PackTracker started successfully.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "❌ Fatal error during startup.");
            MessageBox.Show(
                $"A critical error occurred:\n\n{ex.Message}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    // -------------------------------------------------------------
    // 🛑 Graceful Shutdown
    // -------------------------------------------------------------
    protected override async void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider is not null)
        {
            try
            {
                var apiHost = _serviceProvider.GetService<ApiHostedService>();
                if (apiHost is not null)
                {
                    Log.Information("🛑 Stopping embedded API host...");
                    await apiHost.StopAsync(CancellationToken.None);
                    Log.Information("✅ API host stopped.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "⚠️ Error during shutdown.");
            }
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
