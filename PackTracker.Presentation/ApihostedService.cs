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
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PackTracker.Api.Controllers;
using PackTracker.Api.Middleware;
using PackTracker.Application.Options;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure;
using PackTracker.Infrastructure.Logging;
using PackTracker.Infrastructure.Persistence;
using Serilog;
using CorrelationId;
using CorrelationId.DependencyInjection;

namespace PackTracker.Presentation;

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
                    // ✅ Proper config build (preserve defaults, add secrets + env vars)
                    var config = new ConfigurationBuilder()
                        .AddConfiguration(context.Configuration) // keep appsettings.json & env
                        .AddUserSecrets<ApiHostedService>(optional: true)
                        .AddEnvironmentVariables()
                        .Build();

                    // 🔍 Debug logging
                    Console.WriteLine($"[DEBUG] Jwt:Key loaded? {!string.IsNullOrEmpty(config["Jwt:Key"])}");

                    // Logging + infra
                    services.AddPackTrackerLogging(config);
                    services.AddInfrastructure(config);

                    // Options
                    services.Configure<RegolithOptions>(config.GetSection("Regolith"));

                    // AuthZ policy
                    services.AddAuthorization(options =>
                    {
                        options.AddPolicy("HouseWolfOnly", policy =>
                            policy.RequireClaim(ClaimTypes.Role, "HouseWolfMember"));
                    });

                    services.AddHttpContextAccessor();

                    // OAuth2 + Discord + Cookies
                    services.AddAuthentication(options =>
                        {
                            options.DefaultScheme = "Cookies";
                            options.DefaultChallengeScheme = "Discord";
                        })
                        .AddCookie("Cookies", options =>
                        {
                            options.Cookie.SameSite = SameSiteMode.Lax;
                            options.Cookie.SecurePolicy = CookieSecurePolicy.None; // local http
                        })
                        .AddDiscord("Discord", options =>
                        {
                            options.ClientId = config["Authentication:Discord:ClientId"]!;
                            options.ClientSecret = config["Authentication:Discord:ClientSecret"]!;
                            options.CallbackPath = config["Authentication:Discord:CallbackPath"] ?? "/signin-discord";

                            options.SaveTokens = true;
                            options.Scope.Add("identify");
                            options.Scope.Add("guilds");

                            options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                            options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
                            options.ClaimActions.MapJsonKey("urn:discord:avatar:url", "avatar");

                            options.Events.OnCreatingTicket = async ctx =>
                            {
                                var requiredGuildId = config["Authentication:Discord:RequiredGuildId"];
                                var accessToken = ctx.AccessToken!;

                                using var client = new HttpClient();
                                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                                var guildsResponse = await client.GetAsync("https://discord.com/api/users/@me/guilds");
                                guildsResponse.EnsureSuccessStatusCode();

                                var guildsJson = await guildsResponse.Content.ReadAsStringAsync();
                                var guilds = System.Text.Json.JsonDocument.Parse(guildsJson).RootElement;
                                var guildIds = guilds.EnumerateArray()
                                    .Select(g => g.GetProperty("id").GetString())
                                    .ToList();

                                Console.WriteLine($"[Discord Login] User {ctx.Principal?.Identity?.Name} guilds: {string.Join(",", guildIds)}");

                                bool isMember = guilds.EnumerateArray()
                                    .Any(g => g.GetProperty("id").GetString() == requiredGuildId);

                                if (!isMember)
                                {
                                    throw new Exception("User is not a member of the House Wolf server.");
                                }

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
                            };
                        })
                        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                        {
                            var jwtKey = config["Jwt:Key"];
                            if (string.IsNullOrEmpty(jwtKey))
                                throw new InvalidOperationException("Jwt:Key is not configured.");

                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidateAudience = true,
                                ValidateLifetime = true,
                                ValidateIssuerSigningKey = true,
                                ValidIssuer = config["Jwt:Issuer"],
                                ValidAudience = config["Jwt:Audience"],
                                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                            };
                        });

                    // Correlation ID
                    services.AddDefaultCorrelationId(options =>
                    {
                        options.IncludeInResponse = true;
                        options.UpdateTraceIdentifier = true;
                        options.RequestHeader = "X-Correlation-ID";
                        options.ResponseHeader = "X-Correlation-ID";
                        options.AddToLoggingScope = true;
                    });

                    // EF DbContext
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

                    // MVC + Swagger
                    services.AddControllers()
                        .AddApplicationPart(typeof(ProfilesController).Assembly);

                    services.AddEndpointsApiExplorer();
                    services.AddSwaggerGen(c =>
                    {
                        c.SwaggerDoc("v1", new OpenApiInfo
                        {
                            Title = "🐺 PackTracker API",
                            Version = "v1",
                            Description = "API powering PackTracker — House Wolf’s system for Star Citizen logistics, ops, and data.",
                            Contact = new OpenApiContact
                            {
                                Name = "House Wolf",
                                Url = new Uri("https://housewolf.co")
                            }
                        });

                        var asmName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{asmName}.xml");
                        if (File.Exists(xmlPath))
                            c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

                        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                        {
                            In = ParameterLocation.Header,
                            Description = "JWT Authorization header using the Bearer scheme.",
                            Name = "Authorization",
                            Type = SecuritySchemeType.ApiKey,
                            Scheme = "Bearer"
                        });
                        c.AddSecurityRequirement(new OpenApiSecurityRequirement
                        {
                            {
                                new OpenApiSecurityScheme
                                {
                                    Reference = new OpenApiReference
                                    {
                                        Type = ReferenceType.SecurityScheme,
                                        Id = "Bearer"
                                    }
                                },
                                Array.Empty<string>()
                            }
                        });
                    });

                    services.AddHealthChecks();
                });

                webBuilder.Configure(app =>
                {
                    app.UseStaticFiles();
                    app.UseCorrelationId();

                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();

                    app.UseMiddleware<RequestLoggingMiddleware>();
                    app.UseMiddleware<ExceptionHandlingMiddleware>();

                    app.UseSwagger();
                    app.UseSwaggerUI(c =>
                    {
                        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PackTracker API v1");
                        c.RoutePrefix = "swagger";
                        c.DocumentTitle = "🐺 PackTracker API Docs";
                        c.InjectStylesheet("/swagger-ui/custom.css");
                        c.InjectJavascript("/swagger-ui/custom.js");
                    });

                    app.UseHttpsRedirection();

                    app.UseEndpoints(c =>
                    {
                        c.MapControllers();
                        c.MapHealthChecks("/health");
                    });
                });
            })
            .Build();

        return _apiHost.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        _apiHost?.StopAsync(cancellationToken) ?? Task.CompletedTask;
}
