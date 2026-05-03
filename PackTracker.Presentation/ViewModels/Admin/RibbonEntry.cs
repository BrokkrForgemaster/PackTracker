namespace PackTracker.Presentation.ViewModels.Admin;

public sealed record RibbonEntry(string Name, string Description, string RawImagePath)
{
    public string PackUri => BuildPackUri(RawImagePath);

    private static string BuildPackUri(string path)
    {
        var normalized = path.Replace('\\', '/');
        while (normalized.StartsWith("../") || normalized.StartsWith("./"))
            normalized = normalized[(normalized.IndexOf('/') + 1)..];
        return $"pack://application:,,,/{normalized}";
    }
}
