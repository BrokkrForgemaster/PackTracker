using Serilog.Core;
using Serilog.Events;

namespace PackTracker.Logging.Enrichers;

public sealed class ApplicationEnricher : ILogEventEnricher
{
    private readonly string _applicationName;
    private readonly string _environmentName;

    public ApplicationEnricher(string applicationName, string environmentName)
    {
        _applicationName = string.IsNullOrWhiteSpace(applicationName) ? "PackTracker" : applicationName;
        _environmentName = string.IsNullOrWhiteSpace(environmentName) ? "Production" : environmentName;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Application", _applicationName));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Environment", _environmentName));
    }
}