using Microsoft.Extensions.DependencyInjection;
using PackTracker.Common.Abstractions;
using PackTracker.Infrastructure.Services;

namespace PackTracker.Infrastructure;

/// <summary name="DependencyInjection">
/// Extension methods for registering infrastructure services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped(typeof(ILoggingService<>), typeof(SerilogLoggingService<>));

        return services;
    }
}