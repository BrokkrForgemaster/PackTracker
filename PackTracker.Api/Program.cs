using Microsoft.EntityFrameworkCore;
using PackTracker.Api.Middleware;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure;
using PackTracker.Infrastructure.Logging;
using PackTracker.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Serilog pipeline
builder.Host.UsePackTrackerSerilog();

ISettingsService? settingsService = builder.Services.BuildServiceProvider()
    .GetRequiredService<ISettingsService>();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(settingsService.GetSettings().ConnectionString);
});
builder.Services.AddInfrastructure(settingsService);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
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