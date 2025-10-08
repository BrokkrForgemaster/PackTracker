using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Security;
using PackTracker.Infrastructure.Services;

namespace PackTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, ISettingsService settings)
    {
        var config = settings.GetSettings() ?? throw new InvalidDataException("Application settings could not be loaded.");

        config.ConnectionString ??= string.Empty;
        config.RegolithBaseUrl ??= "https://regolith.rocks/api";
        config.UexBaseUrl ??= "https://api.uexcorp.uk/2.0/";

        // Database
        if (string.IsNullOrWhiteSpace(config.ConnectionString))
            throw new InvalidDataException("Database connection string missing in AppSettings.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(config.ConnectionString);
            options.EnableDetailedErrors();
        });

        // Register settings
        services.AddSingleton(settings);

        // Logging + Security
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped(typeof(ILoggingService<>), typeof(SerilogLoggingService<>));
        services.AddSingleton<JwtTokenService>();
        // HttpClient setup
        services.AddHttpClient<IRegolithService, RegolithService>(client =>
        {
            if (Uri.TryCreate(config.RegolithBaseUrl, UriKind.Absolute, out var uri))
                client.BaseAddress = uri;

            client.DefaultRequestHeaders.UserAgent.ParseAdd("PackTracker/1.0 (+https://housewolf.io)");
        });

        services.AddHttpClient<IUexService, UexService>(client =>
        {
            if (Uri.TryCreate(config.UexBaseUrl, UriKind.Absolute, out var uri))
                client.BaseAddress = uri;

            client.DefaultRequestHeaders.UserAgent.ParseAdd("PackTracker/1.0 (+https://housewolf.io)");
        });
        services.AddSingleton<IRequestsService, RequestsService>();
        services.AddSingleton<IKillEventService, KillEventService>();
        services.AddSingleton<IGameLogService, GameLogService>();

        services.AddMemoryCache();

        return services;
    }
}
