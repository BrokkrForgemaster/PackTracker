using Microsoft.Extensions.Logging;

namespace PackTracker.Presentation.ViewModels;

internal static class ViewModelLoggingExtensions
{
    public static void LogViewModelError<TViewModel>(
        this ILogger<TViewModel> logger,
        Exception exception,
        string operation,
        params (string Key, object? Value)[] context)
    {
        var scope = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ViewModel"] = typeof(TViewModel).Name,
            ["Operation"] = operation
        };

        foreach (var (key, value) in context)
            scope[key] = value;

        using (logger.BeginScope(scope))
        {
            logger.LogError(
                exception,
                "ViewModel operation failed. ViewModel={ViewModel} Operation={Operation}",
                typeof(TViewModel).Name,
                operation);
        }
    }
}
