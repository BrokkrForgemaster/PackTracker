using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication;
using PackTracker.Api.Middleware;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure;
using PackTracker.Infrastructure.Logging;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Security;
using PackTracker.Infrastructure.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Serilog pipeline
builder.Host.UsePackTrackerSerilog();
var tempProvider = builder.Services.BuildServiceProvider();
var settingsLogger = tempProvider.GetRequiredService<ILogger<SettingsService>>();
var settingsService = new SettingsService(settingsLogger);
settingsService.EnsureBootstrapDefaults(builder.Configuration);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(settingsService.GetSettings().ConnectionString);
});
builder.Services.AddSingleton<ISettingsService>(settingsService);
builder.Services.AddInfrastructure(settingsService);
builder.Services.AddScoped(typeof(ILoggingService<>), typeof(SerilogLoggingService<>));
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IKillEventService, KillEventService>();
builder.Services.AddScoped<CraftingSeedService>();
builder.Services.AddMemoryCache();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddSignalR();
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

        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
        options.ClaimActions.MapJsonKey("urn:discord:avatar:url", "avatar");
        options.ClaimActions.MapJsonKey("urn:discord:discriminator", "discriminator");
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appSettings.JwtKey))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("HouseWolfOnly", policy =>
        policy.RequireClaim(ClaimTypes.Role, "HouseWolfMember"));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var scopedServices = scope.ServiceProvider;
    var db = scopedServices.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Apply wiki sync columns idempotently — safe for both fresh installs and
    // existing EnsureCreated-based databases that predate EF migration management.
    var schemaLogger = scopedServices.GetRequiredService<ILogger<AppDbContext>>();
    try
    {
        await db.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE ""Blueprints"" ADD COLUMN IF NOT EXISTS ""WikiUuid"" character varying(200)");
        await db.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE ""Blueprints"" ADD COLUMN IF NOT EXISTS ""WikiLastSyncedAt"" character varying(50)");
        await db.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE ""Materials"" ADD COLUMN IF NOT EXISTS ""WikiUuid"" character varying(200)");
        await db.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE ""Materials"" ADD COLUMN IF NOT EXISTS ""Category"" character varying(100)");
        await db.Database.ExecuteSqlRawAsync(
            @"CREATE INDEX IF NOT EXISTS ""IX_Blueprints_WikiUuid"" ON ""Blueprints"" (""WikiUuid"")");
        await db.Database.ExecuteSqlRawAsync(
            @"CREATE INDEX IF NOT EXISTS ""IX_Materials_WikiUuid"" ON ""Materials"" (""WikiUuid"")");

        // All seeded/synced blueprints are in-game craftable.
        var fixedCount = await db.Database.ExecuteSqlRawAsync(
            @"UPDATE ""Blueprints"" SET ""IsInGameAvailable"" = TRUE WHERE ""IsInGameAvailable"" = FALSE");
        if (fixedCount > 0)
            schemaLogger.LogInformation("Fixed {Count} blueprints with IsInGameAvailable=false", fixedCount);

        // Remove corrupt blueprints written by a previous broken wiki sync
        // (deserialization bug caused WikiUuid='', Category='Unknown', BlueprintName=' Blueprint').
        await db.Database.ExecuteSqlRawAsync(
            @"DELETE FROM ""Blueprints"" WHERE ""WikiUuid"" = ''");

        // Remap raw game type identifiers to user-friendly category names.
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
            )");

        schemaLogger.LogInformation("✅ Blueprint data cleanup applied successfully");
    }
    catch (Exception ex)
    {
        schemaLogger.LogError(ex, "❌ Failed to apply wiki sync schema columns — wiki sync will fail until columns are added manually");
    }

    var seedService = scopedServices.GetRequiredService<CraftingSeedService>();
    var preferredBlueprintPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "scunpacked-data", "blueprints.json"));
    var fallbackSeedPath = Path.Combine(app.Environment.ContentRootPath, "..", "PackTracker.Presentation", "wwwroot", "data", "crafting-seed.json");
    await seedService.SeedAsync(File.Exists(preferredBlueprintPath) ? preferredBlueprintPath : fallbackSeedPath);
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ValidationHandlingMiddleware>();

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
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHub<RequestsHub>(RequestsHub.Route);
    endpoints.MapHealthChecks("/health");
});

try
{
    var logger = app.Services.GetRequiredService<ILoggingService<Program>>();
    logger.LogInformation("🚀 Starting PackTracker API...");
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
