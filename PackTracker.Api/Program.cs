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
