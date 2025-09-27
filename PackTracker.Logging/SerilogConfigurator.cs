// PackTracker.Logging/SerilogConfigurator.cs

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;

namespace PackTracker.Logging;

public static class SerilogConfigurator
{
    // ✅ PRIMARY: works with Generic Host (IHostBuilder) in API, workers, and WPF-hosted apps.
    public static IHostBuilder UsePackTrackerSerilog(this IHostBuilder hostBuilder, string applicationName)
    {
        return hostBuilder.UseSerilog((context, services, loggerCfg) =>
        {
            var cfg = context.Configuration;
            var env = context.HostingEnvironment;
            var opts = LoggingOptions.BindFrom(cfg);

            ConfigureLogger(loggerCfg, opts, applicationName, env.EnvironmentName);
        });
    }

    // ✅ Convenience for Minimal API style (WebApplicationBuilder.Host is an IHostBuilder)
    public static void UseSerilogForHost(this WebApplicationBuilder? builder, string applicationName)
    {
        if (builder != null) builder.Host.UsePackTrackerSerilog(applicationName);
    }


    private static void ConfigureLogger(LoggerConfiguration loggerCfg, LoggingOptions opts, string applicationName, string environmentName)
    {
        loggerCfg
            .MinimumLevel.Is(Parse(opts.MinimumLevel, LogEventLevel.Information))
            .MinimumLevel.Override("Microsoft", Parse(opts.OverrideMicrosoft, LogEventLevel.Warning))
            .MinimumLevel.Override("System", Parse(opts.OverrideSystem, LogEventLevel.Warning))
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", applicationName)
            .Enrich.WithProperty("Environment", environmentName)
            .Enrich.WithExceptionDetails()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId();

        // Console
        if (opts.Console.Enabled)
        {
            loggerCfg = loggerCfg.WriteTo.Console(
                restrictedToMinimumLevel: Parse(opts.Console.MinimumLevel, LogEventLevel.Information));
        }

        // File
        if (opts.File.Enabled)
        {
            var directory = ExpandPath(opts.File.Directory) ?? "./Logs";
            Directory.CreateDirectory(directory);

            var fileName = string.IsNullOrWhiteSpace(opts.File.FileName) ? "packtracker-.json" : opts.File.FileName;

            loggerCfg = loggerCfg.WriteTo.File(
                path: Path.Combine(directory, fileName),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: opts.File.RetainedFileCountLimit ?? 14,
                fileSizeLimitBytes: opts.File.FileSizeLimitBytes ?? (32 * 1024 * 1024),
                rollOnFileSizeLimit: opts.File.RollOnFileSizeLimit ?? true,
                shared: opts.File.Shared ?? true,
                restrictedToMinimumLevel: Parse(opts.File.MinimumLevel, LogEventLevel.Information),
                formatter: new Serilog.Formatting.Compact.CompactJsonFormatter());
        }

        // Seq (optional)
        if (opts.Seq.Enabled && !string.IsNullOrWhiteSpace(opts.Seq.Url))
        {
            loggerCfg = loggerCfg.WriteTo.Seq(
                serverUrl: opts.Seq.Url!,
                apiKey: string.IsNullOrWhiteSpace(opts.Seq.ApiKey) ? null : opts.Seq.ApiKey,
                restrictedToMinimumLevel: Parse(opts.Seq.MinimumLevel, LogEventLevel.Information));
        }

        // OpenTelemetry (optional)
        if (opts.OpenTelemetry.Enabled && !string.IsNullOrWhiteSpace(opts.OpenTelemetry.Endpoint))
        {
            loggerCfg = loggerCfg.WriteTo.OpenTelemetry(o =>
            {
                o.Endpoint = opts.OpenTelemetry.Endpoint!;
                o.Protocol = string.Equals(opts.OpenTelemetry.Protocol, "http", StringComparison.OrdinalIgnoreCase)
                    ? Serilog.Sinks.OpenTelemetry.OtlpProtocol.HttpProtobuf
                    : Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
                o.ResourceAttributes = new Dictionary<string, object?>
                {
                    ["service.name"] = applicationName,
                    ["deployment.environment"] = environmentName
                };
            });
        }
    }

    private static LogEventLevel Parse(string? level, LogEventLevel @default) =>
        Enum.TryParse<LogEventLevel>(level, true, out var parsed) ? parsed : @default;

    private static string? ExpandPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? path : Environment.ExpandEnvironmentVariables(path);
}
