using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using Serilog.Enrichers.CorrelationId;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;

namespace PackTracker.Logging;

public static class PackTrackerLoggingExtensions
{
    public static IHostBuilder UsePackTrackerSerilog(
        this IHostBuilder hostBuilder,
        string applicationName,
        string? logDirectory = null) =>
        hostBuilder.UseSerilog((context, _, configuration) =>
            ConfigureLogger(configuration, context.Configuration, applicationName, logDirectory));

    public static IServiceCollection AddPackTrackerLogging(
        this IServiceCollection services,
        IConfiguration configuration,
        string applicationName,
        string? logDirectory = null)
    {
        Log.Logger = CreateLogger(configuration, applicationName, logDirectory);

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: false);
        });

        services.AddSingleton(typeof(ILoggingService<>), typeof(SerilogLoggingService<>));
        return services;
    }

    public static Serilog.ILogger CreateLogger(
        IConfiguration configuration,
        string applicationName,
        string? logDirectory = null)
    {
        var loggerConfiguration = new LoggerConfiguration();
        ConfigureLogger(loggerConfiguration, configuration, applicationName, logDirectory);
        return loggerConfiguration.CreateLogger();
    }

    private static void ConfigureLogger(
        LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        string applicationName,
        string? logDirectory)
    {
        loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.WithExceptionDetails()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", applicationName)
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithCorrelationId();

        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            return;
        }

        Directory.CreateDirectory(logDirectory);

        loggerConfiguration.WriteTo.Async(writeTo => writeTo.File(
            path: Path.Combine(logDirectory, "packtracker-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            shared: true,
            restrictedToMinimumLevel: LogEventLevel.Information,
            outputTemplate:
            "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}"));
    }
}
