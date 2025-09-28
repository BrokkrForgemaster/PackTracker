using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PackTracker.Application.Interfaces;
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
        var cs =
            Environment.GetEnvironmentVariable("ConnectionStrings:DefaultConnection")
            ?? configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(cs));

        services.AddHttpClient("default");

        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped(typeof(ILoggingService<>), typeof(SerilogLoggingService<>));
        services.AddSingleton<JwtTokenService>();

        // Options binding: if env vars are in config, these already override appsettings via the binder
        services.Configure<RegolithOptions>(configuration.GetSection("Regolith"));

        services.AddMemoryCache();

        services.AddHttpClient<IRegolithService, RegolithService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}