using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.BackgroundServices;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Security;
using PackTracker.Infrastructure.Services;
using PackTracker.Infrastructure.Services.Admin;
using DiscordAnnouncementService = PackTracker.Infrastructure.Services.DiscordAnnouncementService;
using IDiscordAnnouncementService = PackTracker.Application.Interfaces.IDiscordAnnouncementService;

namespace PackTracker.Infrastructure;

/// <summary>
/// Provides extension methods for registering PackTracker infrastructure services.
/// </summary>
public static class DependencyInjection
{
    #region Public Methods

    /// <summary>
    /// Registers infrastructure services, persistence, external clients, caching, and background services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="settings">The application settings service.</param>
    /// <returns>The updated service collection.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the database connection string is missing or invalid.
    /// </exception>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        ISettingsService settings)
    {
        if (settings is null)
            throw new ArgumentNullException(nameof(settings));

        var appSettings = settings.GetSettings();
        var connectionString = appSettings.ConnectionString;
        var apiBaseUrl = appSettings.ApiBaseUrl;

        // Determine if we are using a remote API (Render deployment)
        bool isRemoteApi = Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var uri)
                           && !uri.IsLoopback
                           && uri.Host != "localhost";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (isRemoteApi)
            {
                // Running against Render - local database is not needed/expected
                RegisterCoreServices(services, settings);
                RegisterHttpClients(services, appSettings);
                services.AddMemoryCache();
                return services;
            }

            throw new InvalidOperationException(
                "No database connection string configured. Set it via the Settings view or user secrets.");
        }

        RegisterPersistence(services, connectionString);
        RegisterCoreServices(services, settings);
        RegisterHttpClients(services, appSettings);
        RegisterBackgroundServices(services);

        services.AddMemoryCache();

        return services;
    }

    #endregion

    #region Private Registration Methods

    /// <summary>
    /// Registers the Entity Framework database context and persistence options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    private static void RegisterPersistence(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.EnableDetailedErrors();
            // Snapshot is intentionally behind the model when manual migrations are added
            // without regenerating the snapshot. Tables are created defensively at startup.
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IAdminDbContext>(sp => sp.GetRequiredService<AppDbContext>());
    }

    /// <summary>
    /// Registers core infrastructure services used across the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="settings">The settings service.</param>
    private static void RegisterCoreServices(IServiceCollection services, ISettingsService settings)
    {
        services.AddSingleton(settings);
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IHouseWolfProfileService, HouseWolfProfileService>();
        services.AddScoped<IWikiSyncService, WikiSyncService>();
        services.AddScoped<IDistributedLockService, DatabaseDistributedLockService>();
        services.AddScoped<IDataMaintenanceService, DataMaintenanceService>();
        services.AddScoped<IDatabaseDiagnostics, AppDbContextDiagnostics>();
        services.AddScoped<IRbacService, RbacService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<AdminSeedService>();

        services.AddScoped<JwtTokenService>();
        services.AddSingleton<IRequestsService, RequestsService>();
    }

    /// <summary>
    /// Registers named and typed HTTP clients for supported external integrations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="appSettings">The resolved application settings.</param>
    private static void RegisterHttpClients(IServiceCollection services, PackTracker.Domain.Entities.AppSettings appSettings)
    {
        if (!string.IsNullOrWhiteSpace((string?)appSettings.UexBaseUrl))
        {
            services.AddHttpClient<IUexService, UexService>(client =>
            {
                client.BaseAddress = new Uri(appSettings.UexBaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("PackTracker/0.1.3 (+https://housewolf.io)");
            });
        }
        else
        {
            services.AddHttpClient<IUexService, UexService>(client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("PackTracker/0.1.3 (+https://housewolf.io)");
            });
        }

        services.AddHttpClient<IDiscordAnnouncementService, DiscordAnnouncementService>();

        services.AddHttpClient("WikiApi", client =>
        {
            client.BaseAddress = new Uri("https://api.star-citizen.wiki/api/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PackTracker/0.1.3 (+https://housewolf.io)");
        });
    }

    /// <summary>
    /// Registers hosted background services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    private static void RegisterBackgroundServices(IServiceCollection services)
    {
        services.AddHostedService<WikiSyncBackgroundService>();
        services.AddHostedService<TokenCleanupBackgroundService>();
    }

    #endregion
}
