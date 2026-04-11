namespace PackTracker.Application.Interfaces;

/// <summary>
/// A generic logging service interface that abstracts the underlying logging framework.
/// </summary>
/// <typeparam name="T">The type of the class performing the logging.</typeparam>
public interface ILoggingService<T>
{
    
    void LogInformation(string message, params object[] args);
    void LogInformation(Exception exception, string message, params object[] args);
    
    void LogWarning(string message, params object[] args);
    void LogWarning(Exception exception, string message, params object[] args);
    
    void LogError(string message, params object[] args);
    void LogError(Exception exception, string message, params object[] args);
    
    void LogDebug(string message, params object[] args);
    void LogDebug(Exception exception, string message, params object[] args);
    
    void LogCritical(string message, params object[] args);
    void LogCritical(Exception exception, string message, params object[] args);
}