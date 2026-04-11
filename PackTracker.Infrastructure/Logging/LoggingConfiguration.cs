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
                .Enrich.WithThreadId();
        });
}
