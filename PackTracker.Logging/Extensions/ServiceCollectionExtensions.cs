using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PackTracker.Logging.Configuration;
using PackTracker.Logging.Models;
using Serilog;

namespace PackTracker.Logging.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPackTrackerLogging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<LoggingOptions>(
            configuration.GetSection(LoggingOptions.SectionName));

        Log.Logger = LoggingConfiguration.CreateLogger(configuration);

        services.AddSerilog(Log.Logger, dispose: true);

        return services;
    }
}