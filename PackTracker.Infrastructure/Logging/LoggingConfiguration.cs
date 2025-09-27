using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Exceptions;
using Serilog.Sinks.Async;

namespace PackTracker.Infrastructure.Logging;

public static class LoggingConfiguration
{
    // Call this early (e.g., before Build) to hook Serilog into the Host pipeline
    public static IHostBuilder UsePackTrackerSerilog(this IHostBuilder hostBuilder) =>
        hostBuilder.UseSerilog((context, services, cfg) =>
        {
            cfg.ReadFrom.Configuration(context.Configuration)
                .Enrich.WithExceptionDetails()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .Enrich.FromLogContext()
                .WriteTo.Async(a => a.Console());
        });

    // Optional: adds Serilog as ILogger provider in DI for non-host scenarios
    public static IServiceCollection AddPackTrackerLogging(this IServiceCollection services, IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.WithExceptionDetails()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.FromLogContext()
            .WriteTo.Async(a => a.Console())
            .CreateLogger();

        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddSerilog(Log.Logger, dispose: true);
        });

        return services;
    }
}