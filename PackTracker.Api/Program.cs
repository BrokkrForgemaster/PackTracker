using Microsoft.AspNetCore.DataProtection;
using PackTracker.Api.Hosting;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Services;
using PackTracker.Logging;
using Serilog;
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UsePackTrackerSerilog(
    applicationName: "PackTracker.Api",
    logDirectory: Path.Combine(builder.Environment.ContentRootPath, "Logs"));

var bootstrapLogger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

using var settingsLoggerFactory = LoggerFactory.Create(logging => logging.AddSerilog(bootstrapLogger));
var settingsService = new SettingsService(settingsLoggerFactory.CreateLogger<SettingsService>());

settingsService.EnsureBootstrapDefaults(builder.Configuration);

builder.Services.AddSingleton<ISettingsService>(settingsService);

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("PackTracker");

PackTrackerApiComposition.ConfigureServices(builder, settingsService, isEmbeddedHost: false);

var app = builder.Build();
PackTrackerApiComposition.ConfigurePipeline(app, useHttpsRedirection: true, enableSwaggerUi: true);
await PackTrackerApiComposition.InitializeDatabaseAsync(app, app.Lifetime.ApplicationStopping);

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
