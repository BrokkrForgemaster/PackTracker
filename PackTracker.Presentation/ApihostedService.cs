using System.IO;
using System.Net.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PackTracker.Api.Controllers;
using PackTracker.Api.Middleware;
using PackTracker.Infrastructure;
using PackTracker.Infrastructure.Persistence;
using CorrelationId;
using CorrelationId.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using PackTracker.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;


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
                    context.Configuration = new ConfigurationBuilder()
                        .AddConfiguration(context.Configuration)
                        .AddUserSecrets<ApiHostedService>(optional: true)
                        .Build();

                    services.AddInfrastructure(context.Configuration);
                    services.AddAuthorization();

                    services.AddHttpContextAccessor();
                    services.AddAuthentication(options =>
                        {
                            options.DefaultScheme = "Cookies";
                            options.DefaultChallengeScheme = "Discord";
                        })
                        .AddCookie("Cookies", options =>
                        {
                            options.Cookie.SameSite = SameSiteMode.Lax; // Allow cross-site OAuth redirects
                            options.Cookie.SecurePolicy = CookieSecurePolicy.None; // Because you’re on http://
                        })
                        .AddDiscord("Discord", options =>
                        {
                            var config = context.Configuration;
                            options.ClientId = config["Authentication:Discord:ClientId"]!;
                            options.ClientSecret = config["Authentication:Discord:ClientSecret"]!;
                            options.CallbackPath = config["Authentication:Discord:CallbackPath"] ?? "/signin-discord";
                            

                            options.SaveTokens = true;
                            options.Scope.Add("identify");
                            options.Scope.Add("guilds");

                            options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                            options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
                            options.ClaimActions.MapJsonKey("urn:discord:avatar:url", "avatar");

                            // 🔹 Restrict login to your org's guild
                            options.Events.OnCreatingTicket = async ctx =>
                            {
                                var config = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                                var requiredGuildId = config["Authentication:Discord:RequiredGuildId"];

                                var accessToken = ctx.AccessToken!;
                                using var client = new HttpClient();
                                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                                // Get user guilds
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

                                // ✅ Add custom claim
                                var identity = (ClaimsIdentity)ctx.Principal!.Identity!;
                                identity.AddClaim(new Claim(ClaimTypes.Role, "HouseWolfMember"));

                                // ✅ Upsert into DB
                                var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                                var discordId = ctx.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!;
                                var username = ctx.Principal!.FindFirstValue(ClaimTypes.Name)!;
                                var avatarUrl = ctx.Principal!.FindFirst("urn:discord:avatar:url")?.Value;

                                var profile = await db.Profiles
                                    .SingleOrDefaultAsync(p => p.DiscordId == discordId);

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
                            var config = context.Configuration;
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidateAudience = true,
                                ValidateLifetime = true,
                                ValidateIssuerSigningKey = true,
                                ValidIssuer = config["Jwt:Issuer"],
                                ValidAudience = config["Jwt:Audience"],
                                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!))
                            };
                        });


                    services.AddDefaultCorrelationId(options =>
                    {
                        options.IncludeInResponse = true;
                        options.UpdateTraceIdentifier = true;
                        options.RequestHeader = "X-Correlation-ID";
                        options.ResponseHeader = "X-Correlation-ID";
                        options.AddToLoggingScope = true;
                    });
                    services.AddDbContext<AppDbContext>(options =>
                    {
                        options.UseNpgsql(context.Configuration.GetConnectionString("DefaultConnection"));
                    });

                    services.AddControllers()
                        .AddApplicationPart(typeof(ProfilesController).Assembly);

                    services.AddEndpointsApiExplorer();
                    services.AddSwaggerGen(c =>
                    {
                        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                        {
                            Title = "🐺 PackTracker API",
                            Version = "v1",
                            Description =
                                "API powering PackTracker — House Wolf’s system for Star Citizen logistics, ops, and data.",
                            Contact = new Microsoft.OpenApi.Models.OpenApiContact
                            {
                                Name = "House Wolf",
                                Url = new Uri("https://housewolf.co"),
                            },
                        });

                        var asmName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{asmName}.xml");
                        if (File.Exists(xmlPath))
                        {
                            c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
                        }
                    });

                    services.AddHealthChecks();
                });

                webBuilder.Configure(app =>
                {
                    app.UseStaticFiles();
                    app.UseCorrelationId();

                    app.UseRouting(); // 🔹 Routing must be before auth
                    app.UseAuthentication(); // 🔹 Auth sits after routing
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