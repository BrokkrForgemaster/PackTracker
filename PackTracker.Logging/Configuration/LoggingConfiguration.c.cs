using Microsoft.Extensions.Configuration;
using PackTracker.Logging.Destructuring;
using PackTracker.Logging.Enrichers;
using PackTracker.Logging.Models;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;

namespace PackTracker.Logging.Configuration;

public static class LoggingConfiguration
{
    public static Serilog.ILogger CreateLogger(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new LoggingOptions();
        configuration.GetSection(LoggingOptions.SectionName).Bind(options);

        Directory.CreateDirectory(options.LogDirectory);

        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithExceptionDetails()
            .Enrich.With(new ApplicationEnricher(options.ApplicationName, options.EnvironmentName))
            .Destructure.With<SensitiveDataDestructuringPolicy>()
            .Destructure.With<ExceptionDestructuringPolicy>()
            .Destructure.With<HttpRequestDestructuringPolicy>()
            .Destructure.ToMaximumDepth(5)
            .Destructure.ToMaximumCollectionCount(20)
            .Destructure.ToMaximumStringLength(2048);

        if (options.EnableConsole)
        {
            loggerConfig = loggerConfig.WriteTo.Console(
                outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] [{Application}] {SourceContext} {Message:lj} " +
                "{Properties:j}{NewLine}{Exception}");
        }

        if (options.EnableFile)
        {
            var path = Path.Combine(options.LogDirectory, options.FileNamePrefix);

            loggerConfig = loggerConfig.WriteTo.File(
                path: path,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: options.RetainedFileCountLimit,
                fileSizeLimitBytes: options.FileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                shared: false,
                flushToDiskInterval: TimeSpan.FromSeconds(2),
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{Application}] " +
                "{SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}");
        }

        return loggerConfig.CreateLogger();
    }

    public static LogEventLevel ParseLevel(string? value) =>
        Enum.TryParse<LogEventLevel>(value, true, out var level)
            ? level
            : LogEventLevel.Information;
}