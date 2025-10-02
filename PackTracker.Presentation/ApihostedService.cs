using System.IO;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PackTracker.Api.Controllers;
using PackTracker.Api.Middleware;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;
using PackTracker.Infrastructure;
using PackTracker.Infrastructure.Logging;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Services;

public class ApiHostedService : IHostedService
{
    private IHost? _apiHost;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _apiHost = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls("http://localhost:5001");

                webBuilder.ConfigureServices((context, services) =>
                {
                    // ---- SettingsService (single instance for all)
                    services.AddSingleton<ISettingsService>(sp =>
                    {
                        var logger = sp.GetRequiredService<ILogger<SettingsService>>();
                        var defaultConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                        return new SettingsService(logger, defaultConfigPath);
                    });

                    // Get settings ONCE from DI
                    using var provider = services.BuildServiceProvider();
                    var settingsService = provider.GetRequiredService<ISettingsService>();
                    var settings = settingsService.GetSettings();

                    // ---- Logging
                    services.AddPackTrackerLogging(settingsService);

                    // ---- DbContext
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseNpgsql(settings.ConnectionString));

                    // ---- Infrastructure
                    services.AddInfrastructure(settingsService);

                    // ---- Options objects (Regolith, UEX)
                    services.Configure<RegolithOptions>(opts =>
                    {
                        opts.ApiKey = settings.RegolithApiKey;
                        opts.BaseUrl = settings.RegolithBaseUrl;
                    });

                    services.Configure<UexOptions>(opts =>
                    {
                        opts.ApiKey = settings.UexCorpApiKey;
                        opts.BaseUrl = settings.UexBaseUrl;
                    });

                    // ---- Auth + JWT
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
                        options.ClientId = settings.DiscordClientId;
                        options.ClientSecret = settings.DiscordClientSecret;
                        options.CallbackPath = settings.DiscordCallbackPath ?? "/signin-discord";

                        options.SaveTokens = true;
                        options.Scope.Add("identify");
                        options.Scope.Add("guilds");

                        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
                        options.ClaimActions.MapJsonKey("urn:discord:avatar:url", "avatar");
                    })
                    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                    {
                        if (string.IsNullOrEmpty(settings.JwtKey))
                            throw new InvalidOperationException("Jwt:Key is not configured.");

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

                    // ---- API controllers
                    services.AddControllers()
                        .AddApplicationPart(typeof(ProfilesController).Assembly);

                    services.AddSwaggerGen();
                    services.AddHealthChecks();
                });

                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();

                    app.UseMiddleware<RequestLoggingMiddleware>();
                    app.UseMiddleware<ExceptionHandlingMiddleware>();

                    app.UseSwagger();
                    app.UseSwaggerUI();
                });
            })
            .Build();

        return _apiHost.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        _apiHost?.StopAsync(cancellationToken) ?? Task.CompletedTask;
}
