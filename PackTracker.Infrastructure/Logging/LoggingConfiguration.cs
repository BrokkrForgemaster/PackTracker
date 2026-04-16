using Serilog;
using Serilog.Exceptions;
using Microsoft.Extensions.Hosting;

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
                .Enrich.WithThreadId();
        });
}
