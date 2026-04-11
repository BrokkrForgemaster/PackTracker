using Serilog;
using System.Text;
using System.Security.Claims;
using Serilog.Extensions.Logging;
using PackTracker.Infrastructure;
using PackTracker.Api.Middleware;
using PackTracker.Domain.Entities;
using PackTracker.Api.Hubs;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PackTracker.Infrastructure.Logging;
using PackTracker.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using PackTracker.Infrastructure.Security;
using PackTracker.Infrastructure.Services;
using PackTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;

#region Bootstrap .env

DotNetEnv.Env.TraversePath().Load();

#endregion

#region Builder Setup

var builder = WebApplication.CreateBuilder(args);

builder.Host.UsePackTrackerSerilog();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

#endregion

#region Settings Bootstrap

var bootstrapLogger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var settingsService = new SettingsService(
    new SerilogLoggerFactory(bootstrapLogger)
        .CreateLogger<SettingsService>());

settingsService.EnsureBootstrapDefaults(builder.Configuration);

builder.Services.AddSingleton<ISettingsService>(settingsService);

// Bind strongly-typed options
builder.Services.Configure<PackTracker.Application.Options.AuthOptions>(
    builder.Configuration.GetSection("Authentication"));

#endregion

#region Database

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(settingsService.GetSettings().ConnectionString);
});

#endregion

#region Core Services

builder.Services.AddInfrastructure(settingsService);

builder.Services.AddSingleton(typeof(ILoggingService<>), typeof(SerilogLoggingService<>));
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<CraftingSeedService>();
builder.Services.AddMemoryCache();

#endregion

#region API + Swagger

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

#endregion

#region SignalR (Realtime Foundation)

builder.Services.AddSignalR();

#endregion

#region Authentication

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "Cookies";
        options.DefaultChallengeScheme = "Discord";
    })
    .AddCookie("Cookies", options =>
    {
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
    })
    .AddDiscord("Discord", options =>
    {
        var authOptions = builder.Configuration.GetSection("Authentication")
            .Get<PackTracker.Application.Options.AuthOptions>() ?? new();

        options.ClientId = authOptions.Discord.ClientId;
        options.ClientSecret = authOptions.Discord.ClientSecret;
        options.CallbackPath = authOptions.Discord.CallbackPath ?? "/signin-discord";

        options.SaveTokens = true;
        options.Scope.Add("identify");
        options.Scope.Add("guilds");
        options.Scope.Add("guilds.members.read");

        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
        options.ClaimActions.MapJsonKey("urn:discord:displayname", "global_name");
        options.ClaimActions.MapJsonKey("urn:discord:avatar", "avatar");
        options.ClaimActions.MapJsonKey("urn:discord:discriminator", "discriminator");

        // Behind Render's reverse proxy the app may build the redirect_uri with the wrong
        // scheme (http instead of https) or with an internal port (:8080, :443, etc.).
        // Parse and reconstruct the redirect_uri cleanly so it always matches what's
        // registered in the Discord Developer Portal.
        options.Events.OnRedirectToAuthorizationEndpoint = context =>
        {
            // Split the Discord auth URL query string and find the redirect_uri parameter
            var authUri = new Uri(context.RedirectUri);
            var fixedParts = authUri.Query.TrimStart('?').Split('&').Select(part =>
            {
                if (!part.StartsWith("redirect_uri=", StringComparison.OrdinalIgnoreCase))
                    return part;

                var decoded = Uri.UnescapeDataString(part["redirect_uri=".Length..]);
                var cb = new UriBuilder(decoded);

                // Force https for any non-localhost callback
                if (cb.Scheme == "http" && cb.Host != "localhost")
                    cb.Scheme = "https";

                // Strip any non-standard port (proxy artifacts like :8080, :443, :10000)
                if (cb.Port != -1 && (
                    (cb.Scheme == "https" && cb.Port == 443) ||
                    (cb.Scheme == "http"  && cb.Port == 80)  ||
                    (cb.Port is 8080 or 10000 or 5199)))
                {
                    cb.Port = -1;
                }

                return "redirect_uri=" + Uri.EscapeDataString(cb.Uri.ToString());
            });

            var newUri = new UriBuilder(authUri)
            {
                Query = string.Join("&", fixedParts)
            }.Uri.AbsoluteUri;

            context.Response.Redirect(newUri);
            return Task.CompletedTask;
        };

        options.Events.OnCreatingTicket = ctx =>
        {
            var userId = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var avatar = ctx.User.GetProperty("avatar").GetString();

            if (!string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(avatar))
            {
                var avatarUrl = $"https://cdn.discordapp.com/avatars/{userId}/{avatar}.png";
                ctx.Identity?.AddClaim(new Claim("urn:discord:avatar:url", avatarUrl));
            }

            return Task.CompletedTask;
        };
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        var authOptions = builder.Configuration.GetSection("Authentication")
            .Get<PackTracker.Application.Options.AuthOptions>() ?? new();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = authOptions.Jwt.Issuer,
            ValidAudience = authOptions.Jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(authOptions.Jwt.Key))
        };
    });

#endregion

#region Authorization

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("HouseWolfOnly", policy =>
        policy.RequireClaim(ClaimTypes.Role, "HouseWolfMember"));
});

#endregion

#region Build App

var app = builder.Build();

#endregion

#region Forwarded Headers / Request Debug

// Must run as early as possible so scheme/host are correct behind Render/proxies.
// KnownNetworks/KnownProxies must be explicitly cleared — the { } initializer syntax
// is a no-op on existing collections and does NOT clear the default loopback restriction.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

app.MapGet("/debug-request", (HttpContext ctx) =>
{
    return Results.Json(new
    {
        scheme = ctx.Request.Scheme,
        host = ctx.Request.Host.Value,
        pathBase = ctx.Request.PathBase.Value
    });
});

#endregion

#region Startup Initialization (DB Fix + Seed)

await InitializeDatabaseAsync(app);

#endregion

#region Middleware Pipeline

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ValidationHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

#endregion

#region Endpoints

app.MapControllers();
app.MapHub<RequestsHub>(RequestsHub.Route);
app.MapHealthChecks("/health");

#endregion

#region Run

try
{
    var logger = app.Services.GetRequiredService<ILoggingService<Program>>();

    logger.LogInformation("Starting PackTracker API...");
    logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

#endregion

#region Initialization Method

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();

    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();
    var seedService = scope.ServiceProvider.GetRequiredService<CraftingSeedService>();

    try
    {
        logger.LogInformation("Running database cleanup scripts...");

        var fixedCount = await db.Database.ExecuteSqlRawAsync(
            @"UPDATE ""Blueprints"" SET ""IsInGameAvailable"" = TRUE WHERE ""IsInGameAvailable"" = FALSE");

        if (fixedCount > 0)
            logger.LogInformation("Fixed {Count} blueprint records", fixedCount);

        await db.Database.ExecuteSqlRawAsync(
            @"DELETE FROM ""Blueprints"" WHERE ""WikiUuid"" = ''");

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
            END");

        await db.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE ""CraftingRequests"" ADD COLUMN IF NOT EXISTS ""RequesterTimeZoneDisplayName"" character varying(200)");

        await db.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE ""CraftingRequests"" ADD COLUMN IF NOT EXISTS ""RequesterUtcOffsetMinutes"" integer");

        logger.LogInformation("Database cleanup completed");

        var preferredPath = Path.GetFullPath(
            Path.Combine(app.Environment.ContentRootPath, "..", "scunpacked-data", "blueprints.json"));

        var fallbackPath = Path.Combine(
            app.Environment.ContentRootPath, "..", "PackTracker.Presentation", "wwwroot", "data", "crafting-seed.json");

        var seedPath = File.Exists(preferredPath) ? preferredPath : fallbackPath;

        logger.LogInformation("Seeding crafting data from {Path}", seedPath);

        await seedService.SeedAsync(seedPath);

        logger.LogInformation("Data seeding completed");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Database initialization encountered issues (non-critical)");
    }
}

#endregion