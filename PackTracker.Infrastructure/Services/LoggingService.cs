using Microsoft.Extensions.Logging;
using PackTracker.Common.Abstractions;

namespace PackTracker.Infrastructure.Services;

/// <summary name= "LoggingService">
/// Serilog logging service implementation of <see cref="ILoggingService{T}"/>
/// </summary>
/// <typeparam name="T"></typeparam>
public class SerilogLoggingService<T> : ILoggingService<T>
{
    private readonly ILogger<T> _logger;

    public SerilogLoggingService(ILogger<T> logger)
    {
        _logger = logger;
    }

    public void LogInformation(string message, params object[] args) =>
        _logger.LogInformation(message, args);

    public void LogWarning(string message, params object[] args) =>
        _logger.LogWarning(message, args);

    public void LogError(Exception exception, string message, params object[] args) =>
        _logger.LogError(exception, message, args);

    public void LogDebug(string message, params object[] args) =>
        _logger.LogDebug(message, args);

    public void LogCritical(Exception exception, string message, params object[] args) =>
        _logger.LogCritical(exception, message, args);
}