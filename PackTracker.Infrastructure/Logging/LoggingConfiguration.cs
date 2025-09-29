using Serilog;
using Serilog.Exceptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
               .Enrich.WithCorrelationId()
               .WriteTo.Async(wt =>
               {
                   wt.Console();
                   wt.File("Logs/PackTracker-.log",
                           rollingInterval: RollingInterval.Day,
                           retainedFileCountLimit: 14,
                           fileSizeLimitBytes: 32 * 1024 * 1024,
                           rollOnFileSizeLimit: true,
                           shared: true);
               });
        });

    public static IServiceCollection AddPackTrackerLogging(this IServiceCollection services, IConfiguration configuration)
    {
        // Ensure logger initialized from configuration
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.WithExceptionDetails()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "PackTracker")
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithCorrelationId()
            .WriteTo.Async(a => a.File("Logs/PackTracker-.log", rollingInterval: RollingInterval.Day))
            .CreateLogger();

        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddSerilog(Log.Logger, dispose: true);
        });

        return services;
    }
}
