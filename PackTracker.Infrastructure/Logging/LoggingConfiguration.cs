using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PackTracker.Infrastructure.Logging;
using Serilog;
using Serilog.Exceptions;

namespace PackTracker.Infrastructure.Logging;

public static class LoggingConfiguration
{
    public static IHostBuilder UsePackTrackerSerilog(this IHostBuilder hostBuilder)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        return hostBuilder.UseSerilog((context, services, cfg) =>
        {
            cfg.ReadFrom.Configuration(context.Configuration)
                .Enrich.WithExceptionDetails()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName)
                .WriteTo.Sink(new DatabaseAuditSink(services));
        });
    }

    public static LoggerConfiguration WriteToDatabase(this LoggerConfiguration loggerConfiguration, IServiceProvider serviceProvider)
    {
        return loggerConfiguration.WriteTo.Sink(new DatabaseAuditSink(serviceProvider));
    }
}