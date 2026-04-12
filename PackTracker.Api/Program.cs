using System.Security.Claims;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PackTracker.Api.Authentication;
using PackTracker.Api.Hubs;
using PackTracker.Api.Middleware;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.ApiHosting;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Services;
using PackTracker.Infrastructure.Logging;
using Serilog;
using Serilog.Extensions.Logging;

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

#endregion

#region API Host Services

builder.Services.AddPackTrackerApiHost(settingsService, options =>
{
    options.SmartScheme = ApiAuthenticationDefaults.SmartScheme;
    options.CookieScheme = ApiAuthenticationDefaults.CookieScheme;
    options.DiscordScheme = ApiAuthenticationDefaults.DiscordScheme;
    options.CookieSecurePolicy = CookieSecurePolicy.Always;
    options.SelectScheme = ApiAuthenticationDefaults.SelectScheme;
    options.GetSignalRAccessToken = ApiAuthenticationDefaults.GetSignalRAccessToken;
    options.ConfigureDiscordEvents = events =>
    {
        events.OnRedirectToAuthorizationEndpoint = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggingService<Program>>();
            logger.LogInformation(
                "Discord auth redirect. Scheme={Scheme} Host={Host}",
                context.HttpContext.Request.Scheme,
                context.HttpContext.Request.Host);

            var authUri = new Uri(context.RedirectUri);
            var fixedParts = authUri.Query.TrimStart('?').Split('&').Select(part =>
            {
                if (!part.StartsWith("redirect_uri=", StringComparison.OrdinalIgnoreCase))
                    return part;

                var decoded = Uri.UnescapeDataString(part["redirect_uri=".Length..]);
                var callbackUri = new UriBuilder(decoded);

                if (callbackUri.Scheme == "http" && callbackUri.Host != "localhost")
                    callbackUri.Scheme = "https";

                if (callbackUri.Port != -1 && callbackUri.Port is 80 or 443 or 8080 or 10000 or 5199)
                    callbackUri.Port = -1;

                var fixedUri = callbackUri.Uri.ToString();
                logger.LogInformation("Discord redirect_uri fixed to: {Uri}", fixedUri);
                return "redirect_uri=" + Uri.EscapeDataString(fixedUri);
            });

            var newUri = new UriBuilder(authUri)
            {
                Query = string.Join("&", fixedParts)
            }.Uri.AbsoluteUri;

            context.Response.Redirect(newUri);
            return Task.CompletedTask;
        };

        events.OnRemoteFailure = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggingService<Program>>();
            logger.LogError(
                context.Failure ?? new InvalidOperationException("Unknown remote failure"),
                "Discord remote failure: {Message}",
                context.Failure?.Message ?? "unknown");
            context.Response.Redirect("/auth-error?message=" + Uri.EscapeDataString(context.Failure?.Message ?? "unknown"));
            context.HandleResponse();
            return Task.CompletedTask;
        };
    };
});

builder.Services.AddSwaggerGen();

#endregion

#region Build App

var app = builder.Build();

#endregion

#region Forwarded Headers / Request Debug

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

#region Startup Initialization

await InitializeDatabaseAsync(app, app.Lifetime.ApplicationStopping);

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

static async Task InitializeDatabaseAsync(WebApplication app, CancellationToken ct)
{
    using var scope = app.Services.CreateScope();

    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();
    var seedService = scope.ServiceProvider.GetRequiredService<CraftingSeedService>();

    try
    {
        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync(ct);

        logger.LogInformation("Running database cleanup scripts...");

        var fixedCount = await db.Database.ExecuteSqlRawAsync(
            @"UPDATE ""Blueprints"" SET ""IsInGameAvailable"" = TRUE WHERE ""IsInGameAvailable"" = FALSE",
            ct);

        if (fixedCount > 0)
            logger.LogInformation("Fixed {Count} blueprint records", fixedCount);

        await db.Database.ExecuteSqlRawAsync(
            @"DELETE FROM ""Blueprints"" WHERE ""WikiUuid"" = ''",
            ct);

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
            END",
            ct);

        logger.LogInformation("Database cleanup completed");

        var preferredPath = Path.GetFullPath(
            Path.Combine(app.Environment.ContentRootPath, "..", "scunpacked-data", "blueprints.json"));

        var fallbackPath = Path.Combine(
            app.Environment.ContentRootPath, "..", "PackTracker.Presentation", "wwwroot", "data", "crafting-seed.json");

        var seedPath = File.Exists(preferredPath) ? preferredPath : fallbackPath;

        logger.LogInformation("Seeding crafting data from {Path}", seedPath);

        await seedService.SeedAsync(seedPath, ct);

        logger.LogInformation("Data seeding completed");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Database initialization encountered issues (non-critical)");
    }
}

#endregion
