using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Security;
using PackTracker.Infrastructure.Services;

namespace PackTracker.Infrastructure;

/// <summary>
/// Extension methods for registering infrastructure services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, ISettingsService settingsService)
    {
        var config = settingsService.GetSettings();

        // --- DbContext ---
        services.AddDbContext<AppDbContext>(options =>
        {
            if (string.IsNullOrWhiteSpace(config.ConnectionString))
                throw new InvalidDataException("Database connection string is missing. Please set it in the application settings.");
            options.UseNpgsql(config.ConnectionString);
        });

        // --- Core Services ---
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped(typeof(ILoggingService<>), typeof(SerilogLoggingService<>));
        services.AddSingleton<JwtTokenService>();

        // --- SettingsService ---
        // Don't re-create inside; just register the provided instance
        services.AddSingleton(settingsService);

        // --- Options objects pulled from SettingsService ---
        services.AddSingleton(new RegolithOptions
        {
            ApiKey = config.RegolithApiKey,
            BaseUrl = config.RegolithBaseUrl
        });

        services.AddSingleton(new UexOptions
        {
            ApiKey = config.UexCorpApiKey,
            BaseUrl = config.UexBaseUrl
        });

        // --- HTTP Clients ---
        services.AddHttpClient("default");

        services.AddHttpClient<IRegolithService, RegolithService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<IUexService, UexService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddMemoryCache();

        return services;
    }
}
