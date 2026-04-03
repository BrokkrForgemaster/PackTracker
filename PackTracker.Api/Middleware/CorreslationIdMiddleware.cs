using System.Diagnostics;

namespace PackTracker.Api.Middleware;

/// <summary name="CorrelationIdMiddleware">
/// Middleware to handle correlation IDs for incoming HTTP requests.
/// It checks for an existing correlation ID in the request headers and uses it if present.
/// If not present, it generates a new correlation ID.
/// The correlation ID is added to the response headers and can be used for tracking requests across services.
/// </summary>
public class CorrelationIdMiddleware
{
    #region Constants and Constructors
    
    private const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;
    #endregion
    
    #region Public Methods
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing)
            ? existing.ToString()
            : Activity.Current?.Id ?? Guid.NewGuid().ToString();

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        context.Items[HeaderName] = correlationId;

        await _next(context).ConfigureAwait(false);
    }
    #endregion
}
