using Serilog;
using Serilog.Exceptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using Serilog.Events;

namespace PackTracker.Infrastructure.Logging;

/// <summary name="LoggingConfiguration">
/// Configures Serilog logging for the PackTracker application,
/// including enrichment with contextual information and file logging.
/// </summary>
public static class LoggingConfiguration
{
    
    public static IHostBuilder UsePackTrackerSerilog(this IHostBuilder hostBuilder) =>
        hostBuilder.UseSerilog((context, services, cfg) =>
        {
            cfg.ReadFrom.Configuration(context.Configuration)
                .Enrich.WithExceptionDetails()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "PackTracker")
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .Enrich.WithCorrelationId();
        });

    public static IServiceCollection AddPackTrackerLogging(
        this IServiceCollection services,
        ISettingsService? settingsService)
    {
        // GameLogFilePath is the Star Citizen game log — never use it as the app log path.
        const string logPath = "Logs/packtracker-.log";

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Async(wt =>
            {
                wt.Console();
                wt.File(
                    path: logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    fileSizeLimitBytes: 32 * 1024 * 1024, // numeric here
                    rollOnFileSizeLimit: true,
                    shared: true,
                    outputTemplate:
                    "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}"
                );
            })
            .CreateLogger();

        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddSerilog(Log.Logger, dispose: true);
        });

        return services;
    }
}
