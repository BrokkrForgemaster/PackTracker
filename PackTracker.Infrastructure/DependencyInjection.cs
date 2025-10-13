using System.Security.Cryptography;
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

/// <summary name="DependencyInjection">
/// Extension methods for setting up the infrastructure services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        ISettingsService settings)
    {
        var connFromCfg = configuration.GetConnectionString("DefaultConnection");
        var connFromEnv = Environment.GetEnvironmentVariable("PACKTRACKER__CONNECTIONSTRING");
        var connFromUser = settings?.GetSettings()?.ConnectionString;

        var connectionString =
            !string.IsNullOrWhiteSpace(connFromCfg) ? connFromCfg :
            !string.IsNullOrWhiteSpace(connFromEnv) ? connFromEnv :
            !string.IsNullOrWhiteSpace(connFromUser) ? connFromUser :
            throw new InvalidOperationException("❌ No database connection string configured.");

        Console.WriteLine($"✅ Using connection: {connectionString.Substring(0, Math.Min(40, connectionString.Length))}...");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.EnableDetailedErrors();
        });

        services.AddSingleton(settings);
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped(typeof(ILoggingService<>), typeof(SerilogLoggingService<>));
        services.AddSingleton<JwtTokenService>();

        // 7️⃣ HttpClients
        services.AddHttpClient<IRegolithService, RegolithService>(client =>
        {
            var regolithBaseUrl = settings?.GetSettings().RegolithBaseUrl;
            if (regolithBaseUrl != null) client.BaseAddress = new Uri(regolithBaseUrl);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PackTracker/1.0 (+https://housewolf.io)");
        });

        services.AddHttpClient<IUexService, UexService>(client =>
        {
            var uexBaseUrl = settings?.GetSettings().UexBaseUrl;
            if (uexBaseUrl != null) client.BaseAddress = new Uri(uexBaseUrl);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PackTracker/1.0 (+https://housewolf.io)");
        });

        services.AddSingleton<IRequestsService, RequestsService>();
        services.AddSingleton<IKillEventService, KillEventService>();
        services.AddSingleton<IGameLogService, GameLogService>();
        services.AddHttpClient<IDiscordNotifier, DiscordNotifier>();

        services.AddMemoryCache();
        return services;
    }
}

