using Serilog;
using System.Text;
using System.Security.Claims;
using Serilog.Extensions.Logging;
using PackTracker.Infrastructure;
using PackTracker.Api.Middleware;
using PackTracker.Domain.Entities;
using PackTracker.Api.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PackTracker.Infrastructure.Logging;
using PackTracker.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using PackTracker.Infrastructure.Security;
using PackTracker.Infrastructure.Services;
using PackTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;

#region Builder Setup

var builder = WebApplication.CreateBuilder(args);
builder.Host.UsePackTrackerSerilog();

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

#endregion

#region Database

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(settingsService.GetSettings().ConnectionString);
});

#endregion

#region Core Services

builder.Services.AddInfrastructure(settingsService);

builder.Services.AddScoped(typeof(ILoggingService<>), typeof(SerilogLoggingService<>));
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
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    })
    .AddDiscord("Discord", options =>
    {
        var appSettings = settingsService.GetSettings();

        options.ClientId = appSettings.DiscordClientId!;
        options.ClientSecret = appSettings.DiscordClientSecret!;
        options.CallbackPath = appSettings.DiscordCallbackPath ?? "/signin-discord";

        options.SaveTokens = true;
        options.Scope.Add("identify");
        options.Scope.Add("guilds");
        options.Scope.Add("guilds.members.read");

        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
        options.ClaimActions.MapJsonKey("urn:discord:displayname", "global_name");
        options.ClaimActions.MapJsonKey("urn:discord:avatar", "avatar");
        options.ClaimActions.MapJsonKey("urn:discord:discriminator", "discriminator");

        options.Events.OnCreatingTicket = ctx =>
        {
            var userId = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var avatar = ctx.User.GetProperty("avatar").GetString();
            if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(avatar))
            {
                var avatarUrl = $"https://cdn.discordapp.com/avatars/{userId}/{avatar}.png";
                ctx.Identity?.AddClaim(new Claim("urn:discord:avatar:url", avatarUrl));
            }
            return Task.CompletedTask;
        };
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        var appSettings = settingsService.GetSettings();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = appSettings.JwtIssuer,
            ValidAudience = appSettings.JwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(appSettings.JwtKey))
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

#region Startup Initialization (DB Fix + Seed)

await InitializeDatabaseAsync(app);

#endregion

#region Middleware Pipeline

app.UseMiddleware<CorrelationIdMiddleware>(); // FIRST
app.UseMiddleware<RequestLoggingMiddleware>(); // THEN logging
app.UseMiddleware<ExceptionHandlingMiddleware>(); // THEN exception
app.UseMiddleware<ValidationHandlingMiddleware>(); // THEN validation

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

#endregion

#region Endpoints

app.MapControllers();
app.MapHub<PackTracker.Api.Hubs.RequestsHub>(PackTracker.Api.Hubs.RequestsHub.Route);
app.MapHealthChecks("/health");

#endregion

#region Run

try
{
    var logger = app.Services.GetRequiredService<ILoggingService<Program>>();

    logger.LogInformation("🚀 Starting PackTracker API...");
    logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);

    app.Run();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILoggingService<Program>>();
    logger.LogCritical(ex, "❌ API terminated unexpectedly");
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
        logger.LogInformation("🔧 Running database cleanup scripts...");

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

        logger.LogInformation("✅ Database cleanup completed");

        var preferredPath = Path.GetFullPath(
            Path.Combine(app.Environment.ContentRootPath, "..", "scunpacked-data", "blueprints.json"));

        var fallbackPath = Path.Combine(
            app.Environment.ContentRootPath, "..", "PackTracker.Presentation", "wwwroot", "data", "crafting-seed.json");

        var seedPath = File.Exists(preferredPath) ? preferredPath : fallbackPath;

        logger.LogInformation("🌱 Seeding crafting data from {Path}", seedPath);

        await seedService.SeedAsync(seedPath);

        logger.LogInformation("✅ Data seeding completed");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "⚠️ Database initialization encountered issues (non-critical)");
    }
}

#endregion