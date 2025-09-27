// PackTracker.Presentation/App.xaml.cs
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PackTracker.Infrastructure;
using PackTracker.Infrastructure.Logging;
using Serilog;

namespace PackTracker.Presentation;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder(e.Args)
            .UsePackTrackerSerilog()
            .ConfigureServices((context, services) =>
            {
                services.AddInfrastructure();   // ✅ registers ILoggingService<>
                services.AddSingleton<MainWindow>();
                services.AddHostedService<ApiHostedService>();
            })
            .Build();

        _host.Start();

        _host.Services.GetRequiredService<MainWindow>().Show();
        Log.Information("🖥️ WPF + embedded API started");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("👋 Shutting down");
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}