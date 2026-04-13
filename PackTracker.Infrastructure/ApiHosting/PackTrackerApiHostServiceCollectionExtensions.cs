using System.Security.Claims;
using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;
using PackTracker.Domain.Security;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Services;
using System.Text;

namespace PackTracker.Infrastructure.ApiHosting;

/// <summary>
/// Configures the shared API host services used by both the standalone and embedded API.
/// </summary>
public static class PackTrackerApiHostServiceCollectionExtensions
{
    public static IServiceCollection AddPackTrackerApiHost(
        this IServiceCollection services,
        ISettingsService settingsService,
        Action<PackTrackerApiHostOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settingsService);

        var hostOptions = new PackTrackerApiHostOptions();
        configure?.Invoke(hostOptions);

        var settings = settingsService.GetSettings();

        services.Configure<UexOptions>(options =>
        {
            options.ApiKey = settings.UexCorpApiKey;
            options.BaseUrl = settings.UexBaseUrl;
        });

        services.Configure<AuthOptions>(options =>
        {
            options.Jwt.Key = settings.JwtKey ?? string.Empty;
            options.Jwt.Issuer = settings.JwtIssuer ?? "PackTracker";
            options.Jwt.Audience = settings.JwtAudience ?? "PackTrackerClient";
            options.Discord.ClientId = settings.DiscordClientId ?? string.Empty;
            options.Discord.ClientSecret = settings.DiscordClientSecret ?? string.Empty;
            options.Discord.CallbackPath = settings.DiscordCallbackPath ?? "/signin-discord";
            options.Discord.RequiredGuildId = settings.DiscordRequiredGuildId ?? string.Empty;
            options.Discord.BotToken = settings.DiscordBotToken ?? string.Empty;
        });

        services.AddInfrastructure(settingsService);
        services.AddScoped<CraftingSeedService>();
        services.AddHealthChecks();
        services.AddSignalR();
        services.AddEndpointsApiExplorer();

        var mvcBuilder = services.AddControllers();
        hostOptions.ConfigureControllers?.Invoke(mvcBuilder);

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = hostOptions.SmartScheme;
                options.DefaultAuthenticateScheme = hostOptions.SmartScheme;
                options.DefaultChallengeScheme = hostOptions.DiscordScheme;
            })
            .AddPolicyScheme(
                hostOptions.SmartScheme,
                "JWT or Cookie",
                options =>
                {
                    options.ForwardDefaultSelector = context =>
                        hostOptions.SelectScheme(context);
                })
            .AddCookie(hostOptions.CookieScheme, options =>
            {
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = hostOptions.CookieSecurePolicy;
                options.Cookie.HttpOnly = true;
            })
            .AddDiscord(hostOptions.DiscordScheme, options =>
            {
                options.ClientId = settings.DiscordClientId ?? string.Empty;
                options.ClientSecret = settings.DiscordClientSecret ?? string.Empty;
                options.CallbackPath = settings.DiscordCallbackPath ?? "/signin-discord";

                options.SaveTokens = true;
                options.Scope.Add("identify");
                options.Scope.Add("guilds");
                options.Scope.Add("guilds.members.read");

                options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
                options.ClaimActions.MapJsonKey("urn:discord:displayname", "global_name");
                options.ClaimActions.MapJsonKey("urn:discord:avatar", "avatar");
                options.ClaimActions.MapJsonKey("urn:discord:discriminator", "discriminator");

                var events = new OAuthEvents
                {
                    OnCreatingTicket = context =>
                    {
                        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        if (!string.IsNullOrWhiteSpace(userId)
                            && context.User.TryGetProperty("avatar", out var avatarProperty))
                        {
                            var avatar = avatarProperty.GetString();
                            if (!string.IsNullOrWhiteSpace(avatar))
                            {
                                var avatarUrl = $"https://cdn.discordapp.com/avatars/{userId}/{avatar}.png";
                                context.Identity?.AddClaim(new Claim("urn:discord:avatar:url", avatarUrl));
                            }
                        }

                        return Task.CompletedTask;
                    }
                };

                hostOptions.ConfigureDiscordEvents?.Invoke(events);
                options.Events = events;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var jwtKey = string.IsNullOrWhiteSpace(settings.JwtKey)
                    ? hostOptions.FallbackJwtKey
                    : settings.JwtKey;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = settings.JwtIssuer ?? "PackTracker",
                    ValidAudience = settings.JwtAudience ?? "PackTrackerClient",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = hostOptions.GetSignalRAccessToken(context.Request);
                        if (!string.IsNullOrWhiteSpace(token))
                            context.Token = token;

                        return Task.CompletedTask;
                    }
                };

                hostOptions.ConfigureJwtBearer?.Invoke(options);
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                SecurityConstants.Policies.HouseWolfOnly,
                policy => policy.RequireClaim(ClaimTypes.Role, SecurityConstants.Roles.HouseWolfMember));
        });

        return services;
    }
}

public sealed class PackTrackerApiHostOptions
{
    public string SmartScheme { get; set; } = "Smart";
    public string CookieScheme { get; set; } = "Cookies";
    public string DiscordScheme { get; set; } = "Discord";
    public string FallbackJwtKey { get; set; } = "DytuDWjGyZaCucBzN5OmDFe5SBojkQJBoyK4Y48oDzk=";
    public CookieSecurePolicy CookieSecurePolicy { get; set; } = CookieSecurePolicy.Always;
    public Func<HttpContext, string> SelectScheme { get; set; } = _ => JwtBearerDefaults.AuthenticationScheme;
    public Func<HttpRequest, string?> GetSignalRAccessToken { get; set; } = _ => null;
    public Action<OAuthEvents>? ConfigureDiscordEvents { get; set; }
    public Action<JwtBearerOptions>? ConfigureJwtBearer { get; set; }
    public Action<IMvcBuilder>? ConfigureControllers { get; set; }
}
