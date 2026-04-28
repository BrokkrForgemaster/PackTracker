namespace PackTracker.Application.Options;

public sealed class StartupOptions
{
    public const string SectionName = "Startup";

    public bool FailOnDatabaseInitializationError { get; set; }
}
