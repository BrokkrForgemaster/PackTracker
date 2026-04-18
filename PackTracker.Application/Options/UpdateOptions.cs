namespace PackTracker.Application.Options;

public sealed class UpdateOptions
{
    public const string SectionName = "Updates";

    public string GitHubOwner { get; set; } = "BrokkrForgemaster";
    public string GitHubRepository { get; set; } = "PackTracker";
    public string[] AllowedAssetExtensions { get; set; } = [".exe", ".msi", ".zip"];
    public string UserAgent { get; set; } = "PackTracker-Updater";
    public string? RestartExecutableName { get; set; } = "PackTracker.Presentation.exe";
}
