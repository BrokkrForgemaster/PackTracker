using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities.Admin;
using Serilog.Core;
using Serilog.Events;

namespace PackTracker.Infrastructure.Logging;

/// <summary>
/// A Serilog sink that persists Error and Fatal logs (and specific audit events) to the database.
/// </summary>
public class DatabaseAuditSink : ILogEventSink
{
    private readonly IServiceProvider _serviceProvider;

    public DatabaseAuditSink(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Emit(LogEvent logEvent)
    {
        // We only persist Errors, Fatals, or logs explicitly tagged for Auditing
        var isError = logEvent.Level >= LogEventLevel.Error;
        var isAudit = logEvent.Properties.ContainsKey("AuditEvent");

        if (!isError && !isAudit)
        {
            return;
        }

        // Fire and forget - we don't want to block the logging pipeline
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IAdminDbContext>();

                var auditLog = new AdminAuditLog
                {
                    OccurredAt = logEvent.Timestamp.UtcDateTime,
                    Severity = logEvent.Level.ToString(),
                    Summary = logEvent.RenderMessage(CultureInfo.InvariantCulture),
                    Action = isError ? "ApplicationError" : GetProperty(logEvent, "Action", "SystemEvent") ?? "SystemEvent",
                    TargetType = GetProperty(logEvent, "TargetType", "System") ?? "System",
                    TargetId = GetProperty(logEvent, "TargetId", "N/A") ?? "N/A",
                    CorrelationId = GetProperty(logEvent, "CorrelationId", null),
                    Exception = logEvent.Exception?.ToString(),
                    MachineName = GetProperty(logEvent, "MachineName", Environment.MachineName),
                    Environment = GetProperty(logEvent, "Environment", "Unknown")
                };

                // If ActorProfileId is provided in log context, use it
                if (logEvent.Properties.TryGetValue("ActorProfileId", out var actorValue) && 
                    actorValue is ScalarValue scalar && 
                    scalar.Value is Guid actorId)
                {
                    auditLog.ActorProfileId = actorId;
                }

                dbContext.AdminAuditLogs.Add(auditLog);
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
            catch
            {
                // Self-log failure to avoid infinite recursion if logging the failure also fails
                Serilog.Debugging.SelfLog.WriteLine("Failed to persist audit log to database.");
            }
        });
    }

    private static string? GetProperty(LogEvent logEvent, string propertyName, string? defaultValue = null)
    {
        if (logEvent.Properties.TryGetValue(propertyName, out var value) && value is ScalarValue scalar)
        {
            return scalar.Value?.ToString() ?? defaultValue;
        }
        return defaultValue;
    }
}
