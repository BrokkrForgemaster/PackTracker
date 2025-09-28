using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PackTracker.Application.Interfaces;
using PackTracker.Common.Abstractions;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Security;
using PackTracker.Infrastructure.Services;

namespace PackTracker.Infrastructure;

/// <summary name="DependencyInjection">
/// Extension methods for registering infrastructure services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddHttpClient("default");
        
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped(typeof(ILoggingService<>), typeof(SerilogLoggingService<>));
        services.AddSingleton<JwtTokenService>();


        return services;
    }
}