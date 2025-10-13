using Microsoft.EntityFrameworkCore;
using PackTracker.Api.Middleware;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure;
using PackTracker.Infrastructure.Logging;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Serilog pipeline
builder.Host.UsePackTrackerSerilog();
var settingsService = new SettingsService(builder.Services.BuildServiceProvider()
    .GetRequiredService<ILogger<SettingsService>>());


builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(settingsService.GetSettings().ConnectionString);
});
builder.Services.AddSingleton<ISettingsService>(settingsService);
builder.Services.AddInfrastructure(builder.Configuration, settingsService);
builder.Services.AddScoped(typeof(ILoggingService<>), typeof(SerilogLoggingService<>));


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddSignalR();

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ValidationHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapHub<RequestsHub>(RequestsHub.Route);
app.MapHealthChecks("/health");

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