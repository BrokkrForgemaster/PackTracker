using System.Windows;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;
using PackTracker.Infrastructure;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Services;
using PackTracker.Logging;
using PackTracker.Presentation.Services;
using PackTracker.Presentation.Services.Admin;
using PackTracker.Presentation.Services.Navigation;
using PackTracker.Presentation.ViewModels;
using PackTracker.Presentation.ViewModels.Admin;
using PackTracker.Presentation.Views;
using PackTracker.Presentation.Views.Admin;
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
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HouseWolf",
            "PackTracker");
        var logsPath = Path.Combine(appDataPath, "logs");
        Directory.CreateDirectory(logsPath);

        services.AddPackTrackerLogging(cfg, "PackTracker.Presentation", logsPath);

        // 2️⃣ Settings service (merge environment, secrets, local file)
        var settingsLoggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());
        var settingsLogger = settingsLoggerFactory.CreateLogger<SettingsService>();
        var settingsService = new SettingsService(settingsLogger);
        settingsService.EnsureBootstrapDefaults(cfg);
        services.AddSingleton<ISettingsService>(settingsService);

        // 3️⃣ Infrastructure (requires settings)
        services.AddInfrastructure(settingsService);

        // When using a remote Render API, override IUexService with the HTTP-based
        // implementation so Trading Hub routes through Render instead of hitting Neon DB directly.
        var apiBaseUrl = settingsService.GetSettings().ApiBaseUrl;
        if (Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiUri)
            && !apiUri.IsLoopback
            && apiUri.Host != "localhost")
        {
            services.AddSingleton<IUexService, RemoteUexService>();
        }

        // 4️⃣ Application shell
        services.AddSingleton<System.Windows.Application>(_ => Current);

        // 5️⃣ External API configs
        services.Configure<UexOptions>(cfg.GetSection("Uex"));
        services.Configure<UpdateOptions>(cfg.GetSection(UpdateOptions.SectionName));
        services.AddHttpClient<IUpdateService, UpdateService>();

        // 6️⃣ Core presentation services
        services.AddSingleton<IThemeManager, ThemeManager>();
        services.AddSingleton<AuthTokenService>();
        services.AddSingleton<IApiClientProvider, ApiClientProvider>();
        services.AddSingleton<BackendDiagnosticsService>();
        services.AddSingleton<WikiBlueprintService>();
        services.AddSingleton<IVersionService, VersionService>();
        services.AddSingleton<SignalRChatService>();
        services.AddSingleton<AvatarCacheService>();
        services.AddHttpClient();
        services.AddSingleton<DiscordEventsService>();
        services.AddSingleton<NavigationStateService>();
        services.AddSingleton<AdminApiClient>();
        services.AddSingleton<IConfiguration>(cfg);
        services.AddTransient<DiscordEventsViewModel>();

        // 7️⃣ Views + ViewModels
        services.AddTransient<MainWindow>();
        services.AddSingleton<WelcomeView>();
        services.AddSingleton<LoginView>();
        services.AddTransient<DashboardViewModel>();
        services.AddSingleton<DashboardView>();
        services.AddTransient<RequestsViewModel>();
        services.AddTransient<RequestsView>();
        services.AddTransient<UexViewModel>(sp =>
            new UexViewModel(
                sp.GetRequiredService<IUexService>(),
                sp.GetRequiredService<ILogger<UexViewModel>>()));
        services.AddTransient<UexView>();
        services.AddTransient<BlueprintExplorerViewModel>();
        services.AddTransient<BlueprintExplorerView>();
        services.AddTransient<CraftingRequestsViewModel>();
        services.AddTransient<CraftingRequestsView>();
        services.AddTransient<ProcurementRequestsViewModel>();
        services.AddTransient<ProcurementRequestsView>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<ComponentViewModel>();
        services.AddTransient<AdminShellViewModel>();
        services.AddTransient<AdminDashboardViewModel>();
        services.AddTransient<AdminSettingsViewModel>();
        services.AddTransient<AdminMembersViewModel>();
        services.AddTransient<AdminMedalsViewModel>();
        services.AddTransient<AdminRecruitmentViewModel>();
        services.AddTransient<AdminDashboardView>();
        services.AddTransient<AdminSettingsView>();
        services.AddTransient<AdminMembersView>();
        services.AddTransient<AdminMedalsView>();
        services.AddTransient<AdminRecruitmentView>();
        services.AddTransient<AdminShellView>();

        // Embedded API host — registered as singleton so we can start/stop it manually
        services.AddSingleton<ApiHostedService>();

        // Bind GuideRequest and Api options from configuration
        services.Configure<GuideRequestOptions>(cfg.GetSection(GuideRequestOptions.SectionName));

        services.AddSingleton<GuideNotificationService>();
        services.AddSingleton<GuideRequestWatcher>();
        services.AddSingleton<GuideAssignmentHandler>();
        services.AddTransient<GuideDashboardViewModel>();
        services.AddTransient<NewRequestViewModel>();
        services.AddTransient<ProfileView>();
        services.AddTransient<SettingsView>();
        services.AddSignalR();
        

        return services.BuildServiceProvider();
    }

    // -------------------------------------------------------------
    // 🚀 Application Startup
    // -------------------------------------------------------------
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            DotNetEnv.Env.TraversePath().Load();

            // Load configuration. Each source is optional so a missing or locked file
            // never crashes startup — user settings are persisted separately in %AppData%.
            IConfiguration cfg;
            try
            {
                cfg = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile(src =>
                    {
                        src.Path = "appsettings.json";
                        src.Optional = true;
                        src.ReloadOnChange = false;
                        src.OnLoadException = ctx => ctx.Ignore = true;
                    })
                    .AddUserSecrets<App>(optional: true, reloadOnChange: false)
                    .AddUserSecrets<AppDbContext>(optional: true, reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .Build();
            }
            catch
            {
                // User secrets file corrupt/locked (dev machine only) — fall back to env vars.
                cfg = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile(src =>
                    {
                        src.Path = "appsettings.json";
                        src.Optional = true;
                        src.ReloadOnChange = false;
                        src.OnLoadException = ctx => ctx.Ignore = true;
                    })
                    .AddUserSecrets<AppDbContext>(optional: true, reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .Build();
            }

            // Bootstrap DI
            _serviceProvider = BootstrapServices(cfg);

            var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            logger.LogInformation("🚀 Starting PackTracker...");

            // Start embedded API (fire-and-forget — LoginView polls /health until it's up)
            var apiService = _serviceProvider.GetRequiredService<ApiHostedService>();
            _ = apiService.StartAsync(CancellationToken.None);

            // Load user settings
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            var settings = settingsService.GetSettings();

            // Theme
            var themeManager = _serviceProvider.GetRequiredService<IThemeManager>();
            if (!string.IsNullOrWhiteSpace(settings.Theme))
                themeManager.ApplyTheme(settings.Theme);

            // Setup main window — NavigateToFirstView in the constructor handles routing
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
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
    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.GetService<ApiHostedService>()
            ?.StopAsync(CancellationToken.None)
            .GetAwaiter().GetResult();

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "❌ Unhandled UI thread exception.");
        MessageBox.Show(
            $"A critical error occurred:\n\n{e.Exception.Message}\n\nCheck logs for details.",
            "Runtime Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(-1);
    }
}
