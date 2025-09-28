using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Exceptions;
using Serilog.Enrichers.CorrelationId;

namespace PackTracker.Infrastructure.Logging;

public static class LoggingConfiguration
{
    public static IHostBuilder UsePackTrackerSerilog(this IHostBuilder hostBuilder) =>
        hostBuilder.UseSerilog((context, services, cfg) =>
        {
            cfg.ReadFrom.Configuration(context.Configuration)
                .Enrich.WithExceptionDetails()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "PackTracker")
                .Enrich.WithCorrelationId();

        });

    public static IServiceCollection AddPackTrackerLogging(this IServiceCollection services, IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.WithExceptionDetails()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "PackTracker")
            .Enrich.WithCorrelationId()
            .WriteTo.File( "Logs/PackTracker.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddSerilog(Log.Logger, dispose: true);
        });

        return services;
    }
}