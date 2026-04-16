namespace PackTracker.Logging.Models;

public sealed class LoggingOptions
{
    public const string SectionName = "PackTrackerLogging";

    public string ApplicationName { get; set; } = "PackTracker";
    public string EnvironmentName { get; set; } = "Production";
    public string LogDirectory { get; set; } = "Logs";
    public string FileNamePrefix { get; set; } = "packtracker-.log";
    public string MinimumLevel { get; set; } = "Information";
    public bool EnableConsole { get; set; } = true;
    public bool EnableFile { get; set; } = true;
    public int RetainedFileCountLimit { get; set; } = 14;
    public int FileSizeLimitBytes { get; set; } = 10_485_760; // 10 MB
}