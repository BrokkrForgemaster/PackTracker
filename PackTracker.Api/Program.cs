using Serilog;
using PackTracker.Logging;
using PackTracker.Api.Hosting;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using PackTracker.Infrastructure.Persistence;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

#region Logging Setup
builder.Services.AddPackTrackerLogging(
    configuration: builder.Configuration,
    applicationName: "PackTracker.Api",
    logDirectory: Path.Combine(builder.Environment.ContentRootPath, "Logs"));

var bootstrapLogger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
#endregion

#region Bootstrap Settings
using var settingsLoggerFactory = LoggerFactory.Create(logging => logging.AddSerilog(bootstrapLogger));
var settingsService = new SettingsService(settingsLoggerFactory.CreateLogger<SettingsService>());

settingsService.EnsureBootstrapDefaults(builder.Configuration);

builder.Services.AddSingleton<ISettingsService>(settingsService);
#endregion

#region Data Protection
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("PackTracker");
#endregion

PackTrackerApiComposition.ConfigureServices(builder, settingsService, isEmbeddedHost: false);


var app = builder.Build();

#region Middleware Pipeline
PackTrackerApiComposition.ConfigurePipeline(app, useHttpsRedirection: true, enableSwaggerUi: true);
await PackTrackerApiComposition.InitializeDatabaseAsync(app, app.Lifetime.ApplicationStopping);

try
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

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
