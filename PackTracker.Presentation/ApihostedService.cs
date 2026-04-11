using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PackTracker.Api.Controllers;
using PackTracker.Api.Hubs;
using PackTracker.Api.Middleware;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;
using PackTracker.Infrastructure;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Security;
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

        // If the stored API URL points at a remote host, the app is using the hosted
        // Render deployment — skip starting the embedded server entirely.
        var configuredUrl = _settingsService.GetSettings().ApiBaseUrl;
        if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var configuredUri)
            && !configuredUri.IsLoopback
            && configuredUri.Host != "localhost")
        {
            _logger.LogInformation(
                "Remote API configured at {Url} — embedded host will not start.", configuredUrl);
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

            if (string.IsNullOrWhiteSpace(settings.DiscordClientId) ||
                string.IsNullOrWhiteSpace(settings.DiscordClientSecret))
                throw new InvalidOperationException("Discord OAuth credentials missing.");

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
                        services.AddSingleton(_settingsService);

                        services.AddDataProtection()
                            .PersistKeysToFileSystem(new DirectoryInfo(
                                Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "packtracker",
                                    "apihosted")));

                        services.AddHttpClient();
                        services.AddHealthChecks();
                        services.AddMemoryCache();

                        services.Configure<UexOptions>(o =>
                        {
                            o.ApiKey = settings.UexCorpApiKey;
                            o.BaseUrl = settings.UexBaseUrl;
                        });

                        services.Configure<AuthOptions>(o =>
                        {
                            o.Jwt.Key = settings.JwtKey ?? string.Empty;
                            o.Jwt.Issuer = settings.JwtIssuer ?? "PackTracker";
                            o.Jwt.Audience = settings.JwtAudience ?? "PackTrackerClient";
                            o.Discord.ClientId = settings.DiscordClientId ?? string.Empty;
                            o.Discord.ClientSecret = settings.DiscordClientSecret ?? string.Empty;
                            o.Discord.CallbackPath = settings.DiscordCallbackPath ?? "/signin-discord";
                            o.Discord.RequiredGuildId = settings.DiscordRequiredGuildId ?? string.Empty;
                        });

                        // EF Core DbContext is Scoped by design
                        services.AddDbContext<AppDbContext>(options =>
                            options.UseNpgsql(settings.ConnectionString));

                        // Infrastructure registrations
                        services.AddInfrastructure(_settingsService);

                        services.AddScoped<CraftingSeedService>();

                        services.AddAuthentication(o =>
                            {
                                // Smart scheme: picks JWT for API/SignalR, Cookies for browser flows
                                o.DefaultScheme = "Smart";
                                o.DefaultChallengeScheme = "Discord";
                            })
                            .AddPolicyScheme("Smart", "JWT or Cookie", o =>
                            {
                                o.ForwardDefaultSelector = ctx =>
                                {
                                    var auth = ctx.Request.Headers["Authorization"].FirstOrDefault();
                                    if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer "))
                                        return JwtBearerDefaults.AuthenticationScheme;

                                    if (ctx.Request.Query.ContainsKey("access_token") &&
                                        ctx.Request.Path.StartsWithSegments("/hubs"))
                                        return JwtBearerDefaults.AuthenticationScheme;

                                    return "Cookies";
                                };
                            })
                            .AddCookie("Cookies", o =>
                            {
                                o.Cookie.SameSite = SameSiteMode.Lax;
                                o.Cookie.SecurePolicy = CookieSecurePolicy.None;
                            })
                            .AddDiscord("Discord", o =>
                            {
                                o.ClientId = settings.DiscordClientId!;
                                o.ClientSecret = settings.DiscordClientSecret!;
                                o.CallbackPath = settings.DiscordCallbackPath ?? "/signin-discord";
                                o.SaveTokens = true;
                                o.Scope.Add("identify");
                                o.Scope.Add("guilds");
                                o.Scope.Add("guilds.members.read");

                                o.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                                o.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
                                o.ClaimActions.MapJsonKey("urn:discord:displayname", "global_name");
                                o.ClaimActions.MapJsonKey("urn:discord:avatar", "avatar");
                                o.ClaimActions.MapJsonKey("urn:discord:discriminator", "discriminator");

                                o.Events.OnCreatingTicket = ctx =>
                                {
                                    var userId = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                                    if (!string.IsNullOrEmpty(userId) &&
                                        ctx.User.TryGetProperty("avatar", out var avatarProp))
                                    {
                                        var avatar = avatarProp.GetString();
                                        if (!string.IsNullOrEmpty(avatar))
                                        {
                                            var avatarUrl = $"https://cdn.discordapp.com/avatars/{userId}/{avatar}.png";
                                            ctx.Identity?.AddClaim(new Claim("urn:discord:avatar:url", avatarUrl));
                                        }
                                    }
                                    return Task.CompletedTask;
                                };
                            })
                            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                            {
                                var jwtKey = string.IsNullOrWhiteSpace(settings.JwtKey)
                                    ? "DytuDWjGyZaCucBzN5OmDFe5SBojkQJBoyK4Y48oDzk=" // Fallback to avoid crash
                                    : settings.JwtKey;

                                options.TokenValidationParameters = new TokenValidationParameters
                                {
                                    ValidateIssuer = true,
                                    ValidateAudience = true,
                                    ValidateLifetime = true,
                                    ValidateIssuerSigningKey = true,
                                    ValidIssuer = settings.JwtIssuer,
                                    ValidAudience = settings.JwtAudience,
                                    IssuerSigningKey = new SymmetricSecurityKey(
                                        Encoding.UTF8.GetBytes(jwtKey))
                                };

                                // SignalR WebSocket connections can't send headers —
                                // read the JWT from the access_token query parameter instead.
                                options.Events = new JwtBearerEvents
                                {
                                    OnMessageReceived = ctx =>
                                    {
                                        var token = ctx.Request.Query["access_token"].ToString();
                                        if (!string.IsNullOrEmpty(token) &&
                                            ctx.Request.Path.StartsWithSegments("/hubs"))
                                        {
                                            ctx.Token = token;
                                        }
                                        return Task.CompletedTask;
                                    }
                                };
                            });

                        services.AddAuthorization(options =>
                        {
                            options.AddPolicy("HouseWolfOnly", policy =>
                                policy.RequireClaim(ClaimTypes.Role, "HouseWolfMember"));
                        });

                        services.AddSignalR();

                        services.AddControllers()
                            .AddApplicationPart(typeof(ProfilesController).Assembly);

                        services.AddEndpointsApiExplorer();
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
            db.Database.EnsureCreated();

            await db.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Blueprints"" ADD COLUMN IF NOT EXISTS ""WikiUuid"" character varying(200)", ct);
            await db.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Blueprints"" ADD COLUMN IF NOT EXISTS ""WikiLastSyncedAt"" character varying(50)", ct);
            await db.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Materials"" ADD COLUMN IF NOT EXISTS ""WikiUuid"" character varying(200)", ct);
            await db.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Materials"" ADD COLUMN IF NOT EXISTS ""Category"" character varying(100)", ct);
            await db.Database.ExecuteSqlRawAsync(
                @"CREATE INDEX IF NOT EXISTS ""IX_Blueprints_WikiUuid"" ON ""Blueprints"" (""WikiUuid"")", ct);
            await db.Database.ExecuteSqlRawAsync(
                @"CREATE INDEX IF NOT EXISTS ""IX_Materials_WikiUuid"" ON ""Materials"" (""WikiUuid"")", ct);

            await db.Database.ExecuteSqlRawAsync(
                @"DELETE FROM ""Blueprints"" WHERE ""WikiUuid"" = ''", ct);

            await db.Database.ExecuteSqlRawAsync(
                @"UPDATE ""Blueprints"" SET ""IsInGameAvailable"" = TRUE WHERE ""IsInGameAvailable"" = FALSE", ct);

            // Schema additions added after initial EnsureCreated — safe to run repeatedly
            await db.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""CraftingRequests"" ADD COLUMN IF NOT EXISTS ""MaterialSupplyMode"" integer NOT NULL DEFAULT 2", ct);
            await db.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""CraftingRequests"" ADD COLUMN IF NOT EXISTS ""ItemName"" character varying(300)", ct);

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""AssistanceRequests"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""Kind"" integer NOT NULL DEFAULT 0,
                    ""Title"" character varying(120) NOT NULL,
                    ""Description"" character varying(4000),
                    ""Priority"" integer NOT NULL DEFAULT 1,
                    ""Status"" integer NOT NULL DEFAULT 0,
                    ""CreatedByProfileId"" uuid NOT NULL,
                    ""AssignedToProfileId"" uuid,
                    ""MaterialName"" character varying(100),
                    ""QuantityNeeded"" integer,
                    ""MeetingLocation"" character varying(100),
                    ""RewardOffered"" character varying(100),
                    ""NumberOfHelpersNeeded"" integer,
                    ""DueAt"" timestamp with time zone,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedAt"" timestamp with time zone NOT NULL,
                    ""CompletedAt"" timestamp with time zone
                )", ct);

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
                )", ct);

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
        // Embedded API always binds to localhost — only use the port from the desired URL.
        int port = 5001;

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