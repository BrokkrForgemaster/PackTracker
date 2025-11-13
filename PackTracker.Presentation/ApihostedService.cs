using System.IO;
using System.Net;
using System.Net.Http;
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
using PackTracker.Api.Middleware;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure;
using PackTracker.Infrastructure.Logging;
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
    private string _baseUrl = "http://localhost:5001";

    public ApiHostedService(ISettingsService settingsService, ILogger<ApiHostedService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_apiHost is not null)
            return;

        try
        {
            _baseUrl = "http://localhost:5001"; // hardcode for testing


            _logger.LogInformation("🚀 Initializing embedded PackTracker API on {Url}...", _baseUrl);
            var config = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true,
                    reloadOnChange: true)
                .AddUserSecrets<ApiHostedService>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            var settings = _settingsService.GetSettings();

            // Merge config priorities (Environment > appsettings > user settings)
            settings.JwtKey = config["Jwt:Key"] ?? settings.JwtKey;
            settings.JwtIssuer = config["Jwt:Issuer"] ?? settings.JwtIssuer ?? "PackTracker";
            settings.JwtAudience = config["Jwt:Audience"] ?? settings.JwtAudience ?? "PackTrackerClient";

            settings.ConnectionString = config.GetConnectionString("DefaultConnection") ?? settings.ConnectionString;
            settings.DiscordClientId = config["Authentication:Discord:ClientId"] ?? settings.DiscordClientId;
            settings.DiscordClientSecret =
                config["Authentication:Discord:ClientSecret"] ?? settings.DiscordClientSecret;
            settings.DiscordCallbackPath = config["Authentication:Discord:CallbackPath"] ??
                                           settings.DiscordCallbackPath ?? "/signin-discord";
            settings.DiscordRequiredGuildId =
                config["Authentication:Discord:RequiredGuildId"] ?? settings.DiscordRequiredGuildId;
            settings.RegolithApiKey = config["Regolith:ApiKey"] ?? settings.RegolithApiKey;
            settings.UexCorpApiKey = config["Uex:ApiKey"] ?? settings.UexCorpApiKey;
            settings.UexBaseUrl = config["Uex:BaseUrl"] ?? settings.UexBaseUrl;

            // Validate essentials
            if (string.IsNullOrWhiteSpace(settings.ConnectionString))
                throw new InvalidOperationException("Database connection string missing.");

            if (string.IsNullOrWhiteSpace(settings.JwtKey))
                throw new InvalidOperationException("JWT key missing.");

            if (string.IsNullOrWhiteSpace(settings.DiscordClientId) ||
                string.IsNullOrWhiteSpace(settings.DiscordClientSecret))
                _logger.LogWarning("⚠️ Discord credentials not found — OAuth login may not function.");

            // Build the self-hosted API
            _apiHost = Host.CreateDefaultBuilder()
                .ConfigureLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddSerilog();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls(_baseUrl);
                    _logger.LogInformation("✅ Embedded API running on {Url}/api/v1/Auth/login", _baseUrl);

                    // Configure services

                    webBuilder.UseContentRoot(AppContext.BaseDirectory);
                    webBuilder.UseWebRoot(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddSingleton(_settingsService);
                        services.AddDataProtection()
                            .PersistKeysToFileSystem(new DirectoryInfo(
                                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "packtracker", "apihosted")));

                        services.AddScoped(typeof(ILoggingService<>), typeof(SerilogLoggingService<>));
                        services.AddInfrastructure(_settingsService);

                        // Logging & Core
                        services.AddPackTrackerLogging(_settingsService);
                        services.AddInfrastructure(_settingsService);
                        services.AddHealthChecks();
                        services.AddMemoryCache();

                        // Options pattern
                        services.Configure<RegolithOptions>(o =>
                        {
                            o.ApiKey = settings.RegolithApiKey;
                            o.BaseUrl = settings.RegolithBaseUrl;
                        });
                        services.Configure<UexOptions>(o =>
                        {
                            o.ApiKey = settings.UexCorpApiKey;
                            o.BaseUrl = settings.UexBaseUrl;
                        });

                        // EF Core
                        services.AddDbContext<AppDbContext>(options =>
                            options.UseNpgsql(settings.ConnectionString));

                        // Authentication
                        services.AddAuthentication(o =>
                            {
                                o.DefaultScheme = "Cookies";
                                o.DefaultChallengeScheme = "Discord";
                            })
                            .AddCookie("Cookies", o =>
                            {
                                o.Cookie.SameSite = SameSiteMode.Lax;   // OK for local OAuth
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

                                o.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                                o.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
                                o.ClaimActions.MapJsonKey("urn:discord:avatar:url", "avatar");
                            })
                            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                            {
                                options.TokenValidationParameters = new TokenValidationParameters
                                {
                                    ValidateIssuer = true,
                                    ValidateAudience = true,
                                    ValidateLifetime = true,
                                    ValidateIssuerSigningKey = true,
                                    ValidIssuer = settings.JwtIssuer,
                                    ValidAudience = settings.JwtAudience,
                                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtKey))
                                };
                            });

                        // Authorization
                        services.AddAuthorization(options =>
                        {
                            options.AddPolicy("HouseWolfOnly", policy =>
                                policy.RequireClaim(ClaimTypes.Role, "HouseWolfMember"));
                        });

                        // MVC & Swagger
                        services.AddControllers().AddApplicationPart(typeof(ProfilesController).Assembly);
                        services.AddEndpointsApiExplorer();
                        services.AddSwaggerGen(c =>
                        {
                            c.SwaggerDoc("v1", new OpenApiInfo
                            {
                                Title = "🐺 PackTracker API",
                                Version = "v1",
                                Description =
                                    "Embedded API powering PackTracker — House Wolf’s logistics and data system.",
                                Contact = new OpenApiContact
                                {
                                    Name = "House Wolf",
                                    Url = new Uri("https://housewolf.co")
                                }
                            });
                        });
                    });

                    // Configure HTTP pipeline
                    webBuilder.Configure(app =>
                    {
                        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
                        
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseMiddleware<ExceptionHandlingMiddleware>();
                        
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                            endpoints.MapHealthChecks("/health");
                        });
                    });
                })
                .Build();

            await _apiHost.StartAsync(cancellationToken);
            await _settingsService.UpdateSettingsAsync(s => s.ApiBaseUrl = _baseUrl);

            _logger.LogInformation("✅ Embedded API running at {Url}", _baseUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to start embedded API host.");
            throw;
        }
    }

    private static int GetAvailablePort(int start, int end)
    {
        for (int port = start; port <= end; port++)
        {
            try
            {
                var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch (System.Net.Sockets.SocketException)
            {
                continue;
            }
        }

        throw new IOException($"No available port found between {start} and {end}");
    }

    private string ResolveBaseUrl(string desiredBaseUrl)
    {
        if (!Uri.TryCreate(desiredBaseUrl, UriKind.Absolute, out var uri))
            uri = new Uri("http://localhost:5001");

        var port = uri.Port;
        if (!IsPortAvailable(port))
        {
            _logger.LogWarning("Port {Port} is unavailable. Selecting an alternate port for the embedded API.", port);
            port = GetAvailablePort(port + 1, port + 1000);
        }

        return $"{uri.Scheme}://{uri.Host}:{port}";
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
            _logger.LogInformation("🛑 Stopping embedded API...");
            await _apiHost.StopAsync(cancellationToken);
            await _apiHost.WaitForShutdownAsync(cancellationToken);
            _apiHost.Dispose();
            _logger.LogInformation("✅ Embedded API stopped cleanly.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "⚠️ Error during embedded API shutdown.");
        }
    }
}
