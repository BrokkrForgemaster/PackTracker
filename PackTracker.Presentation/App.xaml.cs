using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;
using PackTracker.Infrastructure;
using PackTracker.Infrastructure.Services;
using PackTracker.Logging.Extensions;
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
            throw new InvalidOperationException("Service provider not initialized.");

        return app._serviceProvider.GetRequiredService<T>();
    }

    private static IServiceProvider BootstrapServices(IConfiguration cfg)
    {
        var services = new ServiceCollection();

        // Logging
        services.AddPackTrackerLogging(cfg);

        // Settings service
        var settingsLoggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(Log.Logger, dispose: false));
        var settingsLogger = settingsLoggerFactory.CreateLogger<SettingsService>();
        var settingsService = new SettingsService(settingsLogger);

        settingsService.EnsureBootstrapDefaults(cfg);
        services.AddSingleton<ISettingsService>(settingsService);

        // Infrastructure
        services.AddInfrastructure(settingsService);

        // Application shell
        services.AddSingleton<System.Windows.Application>(_ => Current);

        // External API configs
        services.Configure<UexOptions>(cfg.GetSection("Uex"));
        services.AddHttpClient<IUexService, UexService>();

        // Core presentation services
        services.AddSingleton<IThemeManager, ThemeManager>();
        services.AddSingleton<AuthTokenService>();
        services.AddSingleton<IApiClientProvider, ApiClientProvider>();
        services.AddSingleton<WikiBlueprintService>();
        services.AddSingleton<IVersionService, VersionService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<SignalRChatService>();

        // Views + ViewModels
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
        services.AddTransient<ComponentViewModel>();

        // Embedded API host
        services.AddSingleton<ApiHostedService>();

        // Configuration-bound options and helpers
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            DotNetEnv.Env.TraversePath().Load();

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
                    .AddEnvironmentVariables()
                    .Build();
            }
            catch
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
                    .AddEnvironmentVariables()
                    .Build();
            }

            _serviceProvider = BootstrapServices(cfg);

            var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Starting PackTracker.");

            var apiService = _serviceProvider.GetRequiredService<ApiHostedService>();
            _ = apiService.StartAsync(CancellationToken.None);

            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            var settings = settingsService.GetSettings();

            var themeManager = _serviceProvider.GetRequiredService<IThemeManager>();
            if (!string.IsNullOrWhiteSpace(settings.Theme))
                themeManager.ApplyTheme(settings.Theme);

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

            if (settings.FirstRunComplete)
                mainWindow.ContentFrame.Navigate(_serviceProvider.GetRequiredService<LoginView>());
            else
                mainWindow.ContentFrame.Navigate(_serviceProvider.GetRequiredService<WelcomeView>());

            mainWindow.Show();
            logger.LogInformation("PackTracker started successfully.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error during startup.");

            MessageBox.Show(
                $"A critical error occurred:\n\n{ex.Message}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _serviceProvider?.GetService<ApiHostedService>()
                ?.StopAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Log.Information("PackTracker shut down.");
        }
        finally
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }

    private void App_DispatcherUnhandledException(
        object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled UI thread exception.");

        MessageBox.Show(
            $"A critical error occurred:\n\n{e.Exception.Message}\n\nCheck logs for details.",
            "Runtime Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
        Shutdown(-1);
    }
}