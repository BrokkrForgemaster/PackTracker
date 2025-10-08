using System.IO;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
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

    public ApiHostedService(ISettingsService settingsService, ILogger<ApiHostedService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("🚀 Initializing embedded PackTracker API...");

            // Load layered config
            var config = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: true)
                .AddUserSecrets<ApiHostedService>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            var settings = _settingsService.GetSettings();

            // Merge config priorities (Environment > appsettings > user settings)
            settings.JwtKey = config["Jwt:Key"] ?? settings.JwtKey;
            settings.ConnectionString = config.GetConnectionString("DefaultConnection") ?? settings.ConnectionString;
            settings.DiscordClientId = config["Authentication:Discord:ClientId"] ?? settings.DiscordClientId;
            settings.DiscordClientSecret = config["Authentication:Discord:ClientSecret"] ?? settings.DiscordClientSecret;
            settings.DiscordCallbackPath = config["Authentication:Discord:CallbackPath"] ?? settings.DiscordCallbackPath ?? "/signin-discord";
            settings.DiscordRequiredGuildId = config["Authentication:Discord:RequiredGuildId"] ?? settings.DiscordRequiredGuildId;
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
                    webBuilder.UseUrls($"http://localhost:5001");
                    _logger.LogInformation("✅ Embedded API running on http://localhost:5001/api/v1/Auth/login");
                  
                    webBuilder.ConfigureServices(services =>
                    {
                        // Logging & Core
                        services.AddPackTrackerLogging(_settingsService);
                        services.AddInfrastructure(_settingsService);

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
                        services.AddAuthentication(options =>
                            {
                                options.DefaultScheme = "Cookies";
                                options.DefaultChallengeScheme = "Discord";
                            })
                            .AddCookie("Cookies", options =>
                            {
                                options.Cookie.SameSite = SameSiteMode.Lax;
                                options.Cookie.SecurePolicy = CookieSecurePolicy.None;
                            })
                            .AddDiscord("Discord", options =>
                            {
                                options.ClientId = settings.DiscordClientId!;
                                options.ClientSecret = settings.DiscordClientSecret!;
                                options.CallbackPath = settings.DiscordCallbackPath ?? "/signin-discord";
                                options.SaveTokens = true;
                                options.Scope.Add("identify");
                                options.Scope.Add("guilds");

                                options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                                options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
                                options.ClaimActions.MapJsonKey("urn:discord:avatar:url", "avatar");

                                options.Events.OnCreatingTicket = async ctx =>
                                {
                                    try
                                    {
                                        var requiredGuildId = settings.DiscordRequiredGuildId;
                                        var accessToken = ctx.AccessToken!;

                                        using var client = new HttpClient();
                                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                                        var guildsResponse = await client.GetAsync("https://discord.com/api/users/@me/guilds");
                                        guildsResponse.EnsureSuccessStatusCode();

                                        var guildsJson = await guildsResponse.Content.ReadAsStringAsync();
                                        var guilds = System.Text.Json.JsonDocument.Parse(guildsJson).RootElement;

                                        bool isMember = string.IsNullOrWhiteSpace(requiredGuildId) ||
                                                        guilds.EnumerateArray().Any(g => g.GetProperty("id").GetString() == requiredGuildId);

                                        if (!isMember)
                                            throw new Exception("User is not a member of the House Wolf server.");

                                        var identity = (ClaimsIdentity)ctx.Principal!.Identity!;
                                        identity.AddClaim(new Claim(ClaimTypes.Role, "HouseWolfMember"));

                                        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                                        var discordId = ctx.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!;
                                        var username = ctx.Principal!.FindFirstValue(ClaimTypes.Name)!;
                                        var avatarUrl = ctx.Principal!.FindFirst("urn:discord:avatar:url")?.Value;

                                        var profile = await db.Profiles.SingleOrDefaultAsync(p => p.DiscordId == discordId);
                                        if (profile == null)
                                        {
                                            profile = new Profile
                                            {
                                                Id = Guid.NewGuid(),
                                                DiscordId = discordId,
                                                Username = username,
                                                AvatarUrl = avatarUrl,
                                                CreatedAt = DateTime.UtcNow,
                                                LastLogin = DateTime.UtcNow
                                            };
                                            db.Profiles.Add(profile);
                                        }
                                        else
                                        {
                                            profile.Username = username;
                                            profile.AvatarUrl = avatarUrl;
                                            profile.LastLogin = DateTime.UtcNow;
                                            db.Profiles.Update(profile);
                                        }

                                        await db.SaveChangesAsync();
                                    }
                                    catch (Exception ex)
                                    {
                                        ctx.Fail(ex.Message);
                                    }
                                };
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
                                Description = "Embedded API powering PackTracker — House Wolf’s logistics and data system.",
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

                        if (env.IsDevelopment())
                            app.UseDeveloperExceptionPage();

                        app.UseRouting();
                        app.UseMiddleware<ExceptionHandlingMiddleware>();

                        app.UseAuthentication();
                        app.UseAuthorization();

                        app.UseSwagger();
                        app.UseSwaggerUI(c =>
                        {
                            c.SwaggerEndpoint("/swagger/v1/swagger.json", "PackTracker API v1");
                            c.RoutePrefix = "swagger";
                        });

                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    });
                })
                .Build();

            await _apiHost.StartAsync(cancellationToken);

            _logger.LogInformation("✅ Embedded API running at http://localhost:5001");
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
