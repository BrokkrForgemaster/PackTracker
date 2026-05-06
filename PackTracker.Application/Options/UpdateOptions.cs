namespace PackTracker.Application.Options;

public sealed class UpdateOptions
{
    public const string SectionName = "Updates";

    public string GitHubOwner { get; set; } = "BrokkrForgemaster";
    public string GitHubRepository { get; set; } = "PackTracker";
    public string[] AllowedAssetExtensions { get; set; } = [".exe", ".msi", ".zip"];
    public string UserAgent { get; set; } = "PackTracker-Updater";
    public string? RestartExecutableName { get; set; } = "PackTracker.Presentation.exe";

    /// <summary>How often the background monitor polls GitHub Releases.</summary>
    public double CheckIntervalHours { get; set; } = 0.25;

    /// <summary>Delay before the first poll after app startup.</summary>
    public double InitialDelaySeconds { get; set; } = 10.0;

    /// <summary>If false, the background monitor never runs.</summary>
    public bool AutoCheckEnabled { get; set; } = true;

    /// <summary>If true, an available update is downloaded in the background automatically.</summary>
    public bool AutoDownload { get; set; } = true;

    /// <summary>How long "Remind me later" suppresses re-prompting.</summary>
    public double RemindLaterHours { get; set; } = 24.0;
}
