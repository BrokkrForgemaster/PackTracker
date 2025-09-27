using PackTracker.Api.Middleware;
using PackTracker.Common.Abstractions;
using PackTracker.Infrastructure;
using PackTracker.Infrastructure.Logging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Serilog pipeline
builder.Host.UsePackTrackerSerilog();

// 🔹 Register application infrastructure
builder.Services.AddInfrastructure();

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