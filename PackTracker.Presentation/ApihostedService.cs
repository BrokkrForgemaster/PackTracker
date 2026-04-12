using System.IO;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using PackTracker.Api.Authentication;
using PackTracker.Api.Controllers;
using PackTracker.Api.Hubs;
using PackTracker.Api.Middleware;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.ApiHosting;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Services;
using Serilog;

namespace PackTracker.Presentation;

/// <summary>
/// Self-hosted embedded API service for PackTracker.
/// Boots Discord + JWT authentication, EFCore, Swagger, and app middleware.
/// </summary>
public class ApiHostedService : IHostedService
{
    private IHost? _apiHost;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ApiHostedService> _logger;
    private readonly string _baseUrl;

    public ApiHostedService(ISettingsService settingsService, ILogger<ApiHostedService> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _baseUrl = ResolveBaseUrl(settingsService.GetSettings().ApiBaseUrl);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_apiHost is not null)
            return;

        var configuredUrl = _settingsService.GetSettings().ApiBaseUrl;
        if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var configuredUri)
            && !configuredUri.IsLoopback
            && configuredUri.Host != "localhost")
        {
            _logger.LogInformation(
                "Remote API configured at {Url} â€” embedded host will not start.",
                configuredUrl);
            return;
        }

        try
        {
            _logger.LogInformation("Initializing embedded PackTracker API on {Url}...", _baseUrl);

            var config = new ConfigurationBuilder()
                .AddJsonFile(src =>
                {
                    src.Path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                    src.Optional = true;
                    src.ReloadOnChange = false;
                    src.OnLoadException = ctx => ctx.Ignore = true;
                })
                .AddUserSecrets<ApiHostedService>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            var settings = _settingsService.GetSettings();

            settings.JwtKey = config["Authentication:Jwt:Key"] ?? settings.JwtKey;
            settings.JwtIssuer = config["Authentication:Jwt:Issuer"] ?? settings.JwtIssuer ?? "PackTracker";
            settings.JwtAudience = config["Authentication:Jwt:Audience"] ?? settings.JwtAudience ?? "PackTrackerClient";
            settings.ConnectionString = config.GetConnectionString("DefaultConnection") ?? settings.ConnectionString;
            settings.DiscordClientId = config["Authentication:Discord:ClientId"] ?? settings.DiscordClientId;
            settings.DiscordClientSecret = config["Authentication:Discord:ClientSecret"] ?? settings.DiscordClientSecret;
            settings.DiscordCallbackPath = config["Authentication:Discord:CallbackPath"] ?? settings.DiscordCallbackPath ?? "/signin-discord";
            settings.DiscordRequiredGuildId = config["Authentication:Discord:RequiredGuildId"] ?? settings.DiscordRequiredGuildId;
            settings.UexCorpApiKey = config["Uex:ApiKey"] ?? settings.UexCorpApiKey;
            settings.UexBaseUrl = config["Uex:ApiBaseUrl"] ?? settings.UexBaseUrl;

            if (string.IsNullOrWhiteSpace(settings.ConnectionString))
                throw new InvalidOperationException("Database connection string missing.");

            if (string.IsNullOrWhiteSpace(settings.JwtKey))
                throw new InvalidOperationException("JWT key missing.");

            if (string.IsNullOrWhiteSpace(settings.DiscordClientId)
                || string.IsNullOrWhiteSpace(settings.DiscordClientSecret))
            {
                throw new InvalidOperationException("Discord OAuth credentials missing.");
            }

            if (string.IsNullOrWhiteSpace(settings.DiscordCallbackPath))
                throw new InvalidOperationException("Discord callback path missing.");

            if (string.IsNullOrWhiteSpace(settings.DiscordRequiredGuildId))
                throw new InvalidOperationException("Discord required guild ID missing.");

            _logger.LogInformation("Configuration loaded and validated successfully. Starting API host...");

            _apiHost = Host.CreateDefaultBuilder()
                .ConfigureLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddSerilog();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls(_baseUrl);
                    webBuilder.UseContentRoot(AppContext.BaseDirectory);
                    webBuilder.UseWebRoot(Path.Combine(AppContext.BaseDirectory, "wwwroot"));

                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddDataProtection()
                            .PersistKeysToFileSystem(new DirectoryInfo(
                                Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "packtracker",
                                    "apihosted")));

                        services.AddPackTrackerApiHost(_settingsService, options =>
                        {
                            options.SmartScheme = ApiAuthenticationDefaults.SmartScheme;
                            options.CookieScheme = ApiAuthenticationDefaults.CookieScheme;
                            options.DiscordScheme = ApiAuthenticationDefaults.DiscordScheme;
                            options.CookieSecurePolicy = CookieSecurePolicy.None;
                            options.SelectScheme = ApiAuthenticationDefaults.SelectScheme;
                            options.GetSignalRAccessToken = ApiAuthenticationDefaults.GetSignalRAccessToken;
                            options.ConfigureControllers = mvc =>
                                mvc.AddApplicationPart(typeof(ProfilesController).Assembly);
                        });

                        services.AddSwaggerGen(c =>
                        {
                            c.SwaggerDoc("v1", new OpenApiInfo
                            {
                                Title = "PackTracker API",
                                Version = "v1",
                                Description = "Embedded API powering PackTracker.",
                                Contact = new OpenApiContact
                                {
                                    Name = "House Wolf",
                                    Url = new Uri("https://housewolf.co")
                                }
                            });
                        });
                    });

                    webBuilder.Configure(app =>
                    {
                        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();

                        app.UseMiddleware<ExceptionHandlingMiddleware>();

                        if (env.IsDevelopment())
                        {
                            app.UseSwagger();
                            app.UseSwaggerUI();
                        }

                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                            endpoints.MapHub<RequestsHub>(RequestsHub.Route);
                            endpoints.MapHealthChecks("/health");
                        });
                    });
                })
                .Build();

            await InitializeDatabaseAsync(cancellationToken);

            await _apiHost.StartAsync(cancellationToken);
            await _settingsService.UpdateSettingsAsync(s => s.ApiBaseUrl = _baseUrl);

            _logger.LogInformation("Embedded API running at {Url}", _baseUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start embedded API host.");
            throw;
        }
    }

    private async Task InitializeDatabaseAsync(CancellationToken ct)
    {
        using var scope = _apiHost!.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var log = sp.GetRequiredService<ILogger<ApiHostedService>>();

        try
        {
            await db.Database.MigrateAsync(ct);

            await db.Database.ExecuteSqlRawAsync(
                @"DELETE FROM ""Blueprints"" WHERE ""WikiUuid"" = ''",
                ct);

            await db.Database.ExecuteSqlRawAsync(
                @"UPDATE ""Blueprints"" SET ""IsInGameAvailable"" = TRUE WHERE ""IsInGameAvailable"" = FALSE",
                ct);

            await db.Database.ExecuteSqlRawAsync(@"
                UPDATE ""Blueprints"" SET ""Category"" = CASE ""Category""
                    WHEN 'WeaponPersonal'        THEN 'Personal Weapon'
                    WHEN 'WeaponAttachment'      THEN 'Weapon Attachment'
                    WHEN 'Char_Armor_Torso'      THEN 'Armor - Torso'
                    WHEN 'Char_Armor_Arms'       THEN 'Armor - Arms'
                    WHEN 'Char_Armor_Legs'       THEN 'Armor - Legs'
                    WHEN 'Char_Armor_Helmet'     THEN 'Armor - Helmet'
                    WHEN 'Char_Armor_Undersuit'  THEN 'Armor - Undersuit'
                    WHEN 'Char_Armor_Backpack'   THEN 'Armor - Backpack'
                    ELSE ""Category""
                END
                WHERE ""Category"" IN (
                    'WeaponPersonal','WeaponAttachment',
                    'Char_Armor_Torso','Char_Armor_Arms','Char_Armor_Legs',
                    'Char_Armor_Helmet','Char_Armor_Undersuit','Char_Armor_Backpack'
                )",
                ct);

            log.LogInformation("Database initialization and cleanup complete.");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Database initialization failed.");
        }

        try
        {
            var seedService = sp.GetRequiredService<CraftingSeedService>();

            var blueprintPath = Path.GetFullPath(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "..", "..", "..", "..",
                    "scunpacked-data",
                    "blueprints.json"));

            var fallbackPath = Path.Combine(AppContext.BaseDirectory, "data", "crafting-seed.json");

            await seedService.SeedAsync(
                File.Exists(blueprintPath) ? blueprintPath : fallbackPath,
                ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Crafting seed failed.");
        }
    }

    private static int GetAvailablePort(int start, int end)
    {
        for (int port = start; port <= end; port++)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch (SocketException)
            {
                continue;
            }
        }

        throw new IOException($"No available port found between {start} and {end}");
    }

    private string ResolveBaseUrl(string desiredBaseUrl)
    {
        var port = 5001;

        if (Uri.TryCreate(desiredBaseUrl, UriKind.Absolute, out var uri) && uri.IsLoopback)
            port = uri.Port > 0 ? uri.Port : 5001;

        if (!IsPortAvailable(port))
        {
            _logger.LogWarning("Port {Port} is unavailable. Selecting alternate port.", port);
            port = GetAvailablePort(port + 1, port + 1000);
        }

        return $"http://localhost:{port}";
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_apiHost == null)
            return;

        try
        {
            _logger.LogInformation("Stopping embedded API...");
            await _apiHost.StopAsync(cancellationToken);
            _apiHost.Dispose();
            _logger.LogInformation("Embedded API stopped cleanly.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during embedded API shutdown.");
        }
    }
}
