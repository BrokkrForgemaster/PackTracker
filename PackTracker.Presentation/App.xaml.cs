using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;
using PackTracker.Infrastructure;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Services;
using PackTracker.Presentation.Services;
using PackTracker.Presentation.ViewModels;
using PackTracker.Presentation.Views;
using Serilog;

namespace PackTracker.Presentation;

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

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            var cfg = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddUserSecrets<App>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(cfg)
                .Enrich.FromLogContext()
                .CreateLogger();

            Log.Information("🔧 Initializing PackTracker services...");

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddSerilog());

            var settingsService = new SettingsService(new LoggerFactory().CreateLogger<SettingsService>());

            services.AddSingleton<System.Windows.Application>(_ => Current);
            // Infrastructure
            services.AddInfrastructure(settingsService);

            // Core
            services.AddSingleton<ISettingsService>(settingsService);
            services.AddSingleton<ApiHostedService>();
            services.AddHostedService(sp => sp.GetRequiredService<ApiHostedService>());
            services.Configure<RegolithOptions>(cfg.GetSection("Regolith"));
            services.AddHttpClient<IRegolithService, RegolithService>();
            services.Configure<UexOptions>(cfg.GetSection("Uex"));
            services.AddHttpClient<IUexService, UexService>();
            // Presentation Services
            services.AddSingleton<IThemeManager, ThemeManager>();

            // Views / ViewModels
            services.AddSingleton<MainWindow>();
            services.AddSingleton<WelcomeView>();
            services.AddSingleton<KillTracker>();
            services.AddSingleton< RefineryJobsView>();
            services.AddSingleton<LoginView>();
            services.AddSingleton<DashboardView>();
            services.AddTransient<UexView>();
            services.AddTransient<UexViewModel>(sp =>
                new UexViewModel(
                    sp.GetRequiredService<IUexService>(),
                    sp.GetRequiredService<ILogger<UexViewModel>>()));
            services.AddTransient<SettingsView>();

            _serviceProvider = services.BuildServiceProvider();

            var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            logger.LogInformation("🚀 Starting PackTracker...");

            // Start embedded API
            var apiHost = _serviceProvider.GetRequiredService<ApiHostedService>();
            await apiHost.StartAsync(CancellationToken.None);

            // UI setup
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            var settings = settingsService.GetSettings();
            var themeManager = _serviceProvider.GetRequiredService<IThemeManager>();

            if (!string.IsNullOrWhiteSpace(settings.Theme))
                themeManager.ApplyTheme(settings.Theme);

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
            MessageBox.Show($"A critical error occurred:\n\n{ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider is not null)
        {
            try
            {
                var apiHost = _serviceProvider.GetService<ApiHostedService>();
                if (apiHost != null)
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
