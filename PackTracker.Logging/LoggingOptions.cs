using Microsoft.Extensions.Configuration;

namespace PackTracker.Logging;

/// <summary>
/// Strongly typed options for logging configuration,
/// bindable from "Logging" section in appsettings.json
/// or other configuration sources.
/// </summary>
public sealed class LoggingOptions
{
    public string? ApplicationName { get; init; }
    public string? MinimumLevel { get; init; } = "Information";
    public string? OverrideMicrosoft { get; init; } = "Warning";
    public string? OverrideSystem { get; init; } = "Warning";

    public ConsoleSink Console { get; init; } = new();
    public FileSink File { get; init; } = new();
    public SeqSink Seq { get; init; } = new();
    public OtelSink OpenTelemetry { get; init; } = new();

    public sealed class ConsoleSink
    {
        public bool Enabled { get; init; } = true;
        public string? MinimumLevel { get; init; } = "Information";
        public bool? UseAnsi { get; init; }
    }

    public sealed class FileSink
    {
        public bool Enabled { get; init; } = true;
        public string? Directory { get; init; } = "./Logs";
        public string? FileName { get; init; } = "packtracker-.json";
        public int? RetainedFileCountLimit { get; init; } = 14;
        public int? FileSizeLimitBytes { get; init; } = 33554432;
        public bool? RollOnFileSizeLimit { get; init; } = true;
        public bool? Shared { get; init; } = true;
        public string? MinimumLevel { get; init; } = "Information";
    }

    /// <summary name="Seq">
    /// Configuration for Seq sink, used if Seq.Enabled is true and Url is provided.
    /// </summary>
    public sealed class SeqSink
    {
        public bool Enabled { get; init; }
        public string? Url { get; init; }
        public string? ApiKey { get; init; }
        public string? MinimumLevel { get; init; } = "Information";
    }

    /// <summary name="OpenTelemetry">
    /// Configuration for OpenTelemetry sink, used if OpenTelemetry.Enabled is true
    /// and Endpoint is provided (OTLP endpoint).
    /// </summary>
    public sealed class OtelSink
    {
        public bool Enabled { get; init; }
        public string? Endpoint { get; init; } // http(s)://host:port or grpc endpoint
        public string? Protocol { get; init; } = "grpc"; // grpc | http
    }

    /// <summary name="BindFrom">
    /// Reads strongly typed options from "Logging" section.
    /// </summary>
    public static LoggingOptions BindFrom(IConfiguration config)
    {
        var opts = new LoggingOptions();
        config.GetSection("Logging").Bind(opts);
        return opts;
    }
}